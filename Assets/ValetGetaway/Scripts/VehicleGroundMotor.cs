using UnityEngine;

namespace ValetGetaway
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class VehicleGroundMotor : MonoBehaviour
    {
        public struct Tuning
        {
            public float acceleration, reverseAcceleration, steering, grip, maxSpeed, braking, alignment;
            /// <summary>How fast the steering angle itself moves, in units of full lock per second. 0 uses the default.</summary>
            public float steerRate;
            /// <summary>The most lateral acceleration the tyres hold before the back steps out. 0 uses grip * 2.2.</summary>
            public float gripLimit;
        }

        [SerializeField, Min(.05f)] float maxStepHeight = .5f;
        [SerializeField, Range(10f, 60f)] float maxSlope = 48f;
        readonly RaycastHit[] hits = new RaycastHit[32];
        Rigidbody body;
        BoxCollider hull;
        PhysicsMaterial slidingMaterial;
        float steerAngle;
        float lateralSlip;
        public bool Grounded { get; private set; }
        public float Speed => body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward);
        /// <summary>Sideways speed across the tyres, m/s: how far the car is sliding rather than rolling.</summary>
        public float Slip => lateralSlip;
        /// <summary>Steering as the wheels actually sit, -1..1 (the input is eased into this).</summary>
        public float SteerAngle => steerAngle;
        /// <summary>Lateral acceleration in g, for body lean and camera feel.</summary>
        public float LateralG { get; private set; }
        /// <summary>Longitudinal acceleration in g (positive under power, negative under braking).</summary>
        public float LongitudinalG { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            hull = GetComponent<BoxCollider>();
            body.centerOfMass = hull.center + Vector3.down * hull.size.y * .4f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = body.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            body.maxDepenetrationVelocity = 3f;
            slidingMaterial = new PhysicsMaterial("Vehicle sliding hull")
            {
                staticFriction = .05f, dynamicFriction = .05f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                hideFlags = HideFlags.DontSave
            };
            hull.sharedMaterial = slidingMaterial;
            // Visual weight transfer: the chassis stays upright for stability, the bodywork leans.
            if (GetComponent<VehicleBodyLean>() == null) gameObject.AddComponent<VehicleBodyLean>();
        }

        void OnDestroy()
        {
            if (slidingMaterial != null) Destroy(slidingMaterial);
        }

        bool StaticSurface(RaycastHit hit)
        {
            return hit.collider != null && hit.collider.attachedRigidbody == null && !hit.collider.isTrigger;
        }

        bool GroundRay(Vector3 origin, float distance, out RaycastHit best)
        {
            best = default;
            float nearest = float.MaxValue;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float minNormal = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!StaticSurface(hit) || hit.normal.y < minNormal || hit.distance >= nearest) continue;
                best = hit;
                nearest = hit.distance;
            }
            return nearest < float.MaxValue;
        }

        bool FindGround(out RaycastHit best)
        {
            best = default;
            float bottom = hull.bounds.min.y;
            float highest = float.NegativeInfinity;
            float lowest = float.PositiveInfinity;
            int flatSupports = 0;
            Vector3 center = hull.bounds.center;
            // Ignore rigidbodies so the probes cannot hit our own hull or another car.
            for (int i = -2; i <= 2; i++)
            {
                var point = Mathf.Abs(i) == 2
                    ? center + transform.right * (hull.size.x * transform.lossyScale.x * .34f * Mathf.Sign(i))
                    : center + transform.forward * (hull.size.z * transform.lossyScale.z * .34f * i);
                point.y = bottom + maxStepHeight + .12f;
                if (!GroundRay(point, maxStepHeight + .52f, out var hit)) continue;
                if (hit.normal.y > .995f) flatSupports++;
                lowest = Mathf.Min(lowest, hit.point.y);
                if (hit.point.y > highest) { best = hit; highest = hit.point.y; }
            }
            // A few centimeters of curb bevel must not pitch the whole chassis like a launch ramp.
            if (flatSupports >= 2 && highest - lowest <= maxStepHeight && best.normal.y > .9f) best.normal = Vector3.up;
            return highest > float.NegativeInfinity && (body.linearVelocity.y < 2f
                || best.normal.y < .995f && bottom - highest < .15f);
        }

        bool HullBlocked(Vector3 center, Vector3 direction, float distance)
        {
            Vector3 half = Vector3.Scale(hull.size * .5f, transform.lossyScale) - Vector3.one * .025f;
            int count = Physics.BoxCastNonAlloc(center, half, direction, hits, body.rotation, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == body) continue;
                if (direction.y > .9f && hit.point.y < hull.bounds.max.y - .02f) continue;
                if (direction.y > .9f && hit.distance < .005f) continue;
                if (Vector3.Dot(hit.normal, direction) < -.05f) return true;
            }
            return false;
        }

        void StepOverCurb(Vector3 direction, RaycastHit support)
        {
            var bounds = hull.bounds;
            float floor = bounds.min.y;
            float distance = Mathf.Clamp(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * Time.fixedDeltaTime + .15f, .2f, .8f);
            float reach = Mathf.Abs(Vector3.Dot(direction, transform.forward)) * hull.size.z * transform.lossyScale.z * .5f
                + Mathf.Abs(Vector3.Dot(direction, transform.right)) * hull.size.x * transform.lossyScale.x * .5f;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float underbodyRise = support.point.y - floor;
            float needed = underbodyRise > .005f && underbodyRise <= maxStepHeight ? underbodyRise + .025f : 0f;
            // Include both bumper corners, diagonal travel, and reversing over a curb.
            for (int lane = -1; lane <= 1; lane++)
            {
                // Imported road meshes have no solid side below the raised asphalt.
                // Probe the top ahead of the bumper instead of relying on a low wall ray.
                Vector3 above = bounds.center + direction * (reach + distance)
                    + side * hull.size.x * transform.lossyScale.x * .36f * lane;
                above.y = floor + maxStepHeight + .1f;
                if (!GroundRay(above, maxStepHeight + .12f, out var top)) continue;
                float rise = top.point.y - floor;
                if (rise < .005f || rise > maxStepHeight) continue;
                needed = Mathf.Max(needed, rise + .025f);
            }
            if (needed <= 0f) return;
            Vector3 center = transform.TransformPoint(hull.center);
            if (HullBlocked(center, Vector3.up, needed) || HullBlocked(center + Vector3.up * needed, direction, distance)) return;
            body.position += Vector3.up * needed;
            Vector3 velocity = body.linearVelocity;
            if (velocity.y < 0f) velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        public bool IsTraversableContact(ContactPoint contact, Collider obstacle)
        {
            if (obstacle == null || obstacle.attachedRigidbody != null) return false;
            float bottom = hull.bounds.min.y;
            if (contact.point.y > bottom + maxStepHeight || contact.normal.y > .65f) return false;
            Vector3 above = contact.point - contact.normal * .12f;
            above.y = bottom + maxStepHeight + .1f;
            return GroundRay(above, maxStepHeight + .15f, out var top)
                && top.collider == obstacle && top.point.y - bottom <= maxStepHeight;
        }

        public void Drive(float throttle, float steer, bool brake, Tuning tuning) =>
            Drive(throttle, steer, brake, 0f, tuning);

        /// <summary>
        /// One physics step of driving. <paramref name="handbrake"/> (0..1) unloads the tyres sideways and drags the car
        /// down, so a yank of it rotates the car into a slide the way a handbrake turn does, instead of just stopping.
        /// </summary>
        public void Drive(float throttle, float steer, bool brake, float handbrake, Tuning tuning)
        {
            if (body.isKinematic) return;
            handbrake = Mathf.Clamp01(handbrake);
            throttle = Mathf.Clamp(throttle, -1f, 1f);
            steer = Mathf.Clamp(steer, -1f, 1f);
            Grounded = FindGround(out var ground);
            // Mesh bevel contacts can inject upward speed after the physics step.
            // Settle that impulse while the wheels are still beside flat pavement.
            if (ground.collider != null && ground.normal.y > .995f
                && hull.bounds.min.y - ground.point.y < .35f && body.linearVelocity.y > 0f)
            {
                var settled = body.linearVelocity;
                settled.y = 0f;
                body.linearVelocity = settled;
                Grounded = true;
            }
            if (!Grounded) return;
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (ground.normal.y < .995f)
            {
                // Virtual wheel contact redirects travel along a slope and carries that
                // upward momentum beyond the ramp lip, instead of stepping up it flat.
                velocity.y = -Vector3.Dot(horizontal, ground.normal) / ground.normal.y;
                body.linearVelocity = velocity;
            }
            if (ground.normal.y >= .995f && !brake && (Mathf.Abs(throttle) > .01f || horizontal.sqrMagnitude > 1f))
            {
                Vector3 travel = horizontal.sqrMagnitude > .56f ? horizontal.normalized : transform.forward * Mathf.Sign(throttle);
                StepOverCurb(travel, ground);
            }
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, ground.normal).normalized;
            Vector3 side = Vector3.Cross(ground.normal, forward).normalized;
            float speed = Vector3.Dot(velocity, forward);
            float dt = Time.fixedDeltaTime;
            float top = Mathf.Max(1f, tuning.maxSpeed);

            // ---- Steering: the wheels take time to turn, and they bite less the faster you are going. ----
            float rate = tuning.steerRate > 0f ? tuning.steerRate : 5f;
            // Returning to centre is quicker than winding lock on, which is what makes a car feel like it has a
            // self-centring rack rather than a tank turret.
            float toward = Mathf.Abs(steer) < Mathf.Abs(steerAngle) || steer * steerAngle < 0f ? rate * 1.8f : rate;
            steerAngle = Mathf.MoveTowards(steerAngle, Mathf.Clamp(steer, -1f, 1f), toward * dt);

            // ---- Longitudinal: a torque curve, engine braking, and a brake that is not a wall. ----
            bool changingDirection = throttle * speed < -.5f;
            float limit = throttle < 0f ? top * .5f : top;
            float fade = 1f - Mathf.Clamp01(Mathf.Abs(speed) / limit);
            float torque = Mathf.Lerp(.4f, 1f, Mathf.Pow(fade, .6f));     // strong off the line, tapering near top speed
            if (brake || changingDirection)
                body.AddForce(-horizontal * tuning.braking, ForceMode.Acceleration);
            else if (Mathf.Abs(throttle) > .01f && Mathf.Abs(speed) < limit)
                body.AddForce(forward * (throttle * torque * (throttle > 0f ? tuning.acceleration : tuning.reverseAcceleration)), ForceMode.Acceleration);
            else if (Mathf.Abs(throttle) < .01f)
                body.AddForce(-horizontal * Mathf.Lerp(.35f, 1.1f, Mathf.Clamp01(Mathf.Abs(speed) / top)), ForceMode.Acceleration);
            if (handbrake > 0f) body.AddForce(-horizontal * (tuning.braking * .45f * handbrake), ForceMode.Acceleration);

            // ---- Yaw: speed-sensitive, and freer while the back end is light. ----
            float steerFactor = Mathf.Clamp01(Mathf.Abs(speed) / 2f) * Mathf.Lerp(1f, .42f, Mathf.Clamp01(Mathf.Abs(speed) / top));
            steerFactor *= Mathf.Lerp(1f, 1.45f, handbrake);
            float yaw = steerAngle * tuning.steering * steerFactor * Mathf.Sign(speed) * dt;
            // Restore the flat-road attitude after leaving a slope, without fighting steering.
            var tilted = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(forward, ground.normal), Mathf.Clamp01(tuning.alignment * dt));
            body.MoveRotation(Quaternion.AngleAxis(yaw, ground.normal) * tilted);

            // ---- Grip: tyres hold a finite sideways force, so a fast corner washes wide instead of being on rails. ----
            float gripAccel = tuning.grip > 0f ? tuning.grip : 6f;
            float gripLimit = tuning.gripLimit > 0f ? tuning.gripLimit : gripAccel * 2.2f;
            gripLimit *= Mathf.Lerp(1f, .25f, handbrake);
            lateralSlip = Vector3.Dot(body.linearVelocity, side);
            float hold = Mathf.Clamp(-lateralSlip * gripAccel, -gripLimit, gripLimit);
            body.AddForce(side * hold, ForceMode.Acceleration);

            LateralG = hold / 9.81f;
            LongitudinalG = (Vector3.Dot(body.linearVelocity, forward) - speed) / dt / 9.81f;

            horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float cap = speed < -.5f ? top * .5f : top;
            if (horizontal.magnitude > cap) body.linearVelocity = horizontal.normalized * cap + Vector3.up * body.linearVelocity.y;
        }
    }
}
