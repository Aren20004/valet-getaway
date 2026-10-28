
using UnityEngine;

namespace ValetGetaway
{
    [RequireComponent(typeof(Rigidbody), typeof(VehicleGroundMotor), typeof(PoliceRam))]
    public class PoliceCarAI : MonoBehaviour
    {
        public ThirdPersonPlayerController player;
        public float acceleration = 18f;
        public float turnSpeed = 100f;
        public float maxSpeed = 20f;

        private Rigidbody rb;
        private VehicleGroundMotor motor;
        private float stuckTime;
        private float reversingUntil;
        private Vector3[] dispatchRoute;
        private int routeIndex;
        private Vector3 lastRoutePosition;
        private float routeStallTime;
        private bool clearingBay;
        private float nextShortcutCheck;
        public bool PursuitEnabled { get; set; } = true;
        public Vector3 TargetOffset { get; set; }
        public bool HasDispatchRoute => dispatchRoute != null && routeIndex < dispatchRoute.Length;
        public Vector3 CurrentRouteTarget => HasDispatchRoute ? dispatchRoute[routeIndex] : transform.position;
        public bool BlockingParkingAisle => clearingBay && HasDispatchRoute && routeIndex < 2
            && (routeIndex == 0 || Vector3.Distance(transform.position, dispatchRoute[0]) < 3f);
        public bool HasClearedParkingExit => !clearingBay || routeIndex >= 3;

        public void SetDispatchRoute(Vector3[] points, bool leavingBay = false)
        {
            dispatchRoute = points;
            routeIndex = 0;
            routeStallTime = 0f;
            lastRoutePosition = transform.position;
            clearingBay = leavingBay;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            motor = GetComponent<VehicleGroundMotor>();
            if (motor == null) motor = gameObject.AddComponent<VehicleGroundMotor>();
            if (GetComponent<PoliceRam>() == null) gameObject.AddComponent<PoliceRam>();
        }

        private void OnDisable()
        {
            if (rb == null || rb.isKinematic) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            stuckTime = 0f;
            reversingUntil = 0f;
        }

        private void FixedUpdate()
        {
            if (player == null || !player.IsDriving || !PursuitEnabled)
            {
                motor.Drive(0f, 0f, true, Tuning(maxSpeed));
                return;
            }

            while (HasDispatchRoute && Vector3.ProjectOnPlane(dispatchRoute[routeIndex] - transform.position, Vector3.up).sqrMagnitude < (clearingBay && routeIndex == 0 ? 1f : 6.25f))
                routeIndex++;
            if (HasDispatchRoute && routeIndex > 0)
            {
                Vector3 segment = Vector3.ProjectOnPlane(dispatchRoute[routeIndex] - dispatchRoute[routeIndex - 1], Vector3.up).normalized;
                Vector3 beyond = Vector3.ProjectOnPlane(transform.position - dispatchRoute[routeIndex], Vector3.up);
                if (Vector3.Dot(beyond, segment) > 0f && Vector3.Cross(beyond, segment).magnitude < 4f) routeIndex++;
            }

            // With the helicopter holding the player in its light, the cars are called straight to that position
            // instead of each one guessing from its own last sighting.
            var chase = PoliceChase.Current;
            Vector3 quarry = chase != null && chase.Spotted ? chase.SpottedPosition : player.transform.position;
            if (!HasDispatchRoute && chase != null && chase.PlayerBody != null)
                quarry += Vector3.ClampMagnitude(chase.PlayerBody.linearVelocity * Mathf.Clamp(Vector3.Distance(quarry, transform.position) / 35f, 0f, 1.1f), 16f);
            // Once outside the gate, a clear land route beats completing an unnecessary loop.
            if (clearingBay && HasDispatchRoute && routeIndex >= 3 && Time.time >= nextShortcutCheck)
            {
                nextShortcutCheck = Time.time + .75f;
                if (ClearRoute(quarry) && GroundAlongRoute(quarry)) SetDispatchRoute(null);
            }

            // A missed corner must not trap a cruiser circling one waypoint forever.
            if (HasDispatchRoute)
            {
                if (Vector3.Distance(transform.position, lastRoutePosition) > 3f)
                { lastRoutePosition = transform.position; routeStallTime = 0f; }
                else routeStallTime += Time.fixedDeltaTime;
                if (routeStallTime > 8f && routeIndex + 1 < dispatchRoute.Length
                    && ClearRoute(dispatchRoute[routeIndex + 1]))
                { routeIndex++; routeStallTime = 0f; }
            }

            Vector3 direction =
                (HasDispatchRoute ? dispatchRoute[routeIndex] : quarry + TargetOffset) - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                return;

            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            if (motor.Grounded && Mathf.Abs(motor.Speed) < .4f) stuckTime += Time.fixedDeltaTime;
            else stuckTime = 0f;
            if (stuckTime > 1.25f) { reversingUntil = Time.time + 1f; stuckTime = 0f; }
            bool reverse = Time.time < reversingUntil || direction.sqrMagnitude < 64f && Mathf.Abs(angle) > 110f;
            float turn = reverse ? -Vector3.SignedAngle(-transform.forward, direction, Vector3.up) / 45f : angle / 45f;
            float throttle = reverse ? -1f : Mathf.Lerp(1f, .45f, Mathf.Clamp01(Mathf.Abs(angle) / 90f));
            float speedLimit = HasDispatchRoute ? Mathf.Lerp(4f, maxSpeed, Mathf.Clamp01((direction.magnitude - 3f) / 25f)) : maxSpeed;
            if (Mathf.Abs(angle) > 35f) speedLimit = Mathf.Min(speedLimit, HasDispatchRoute ? 5f : 10f);
            if (HasDispatchRoute && routeIndex + 1 < dispatchRoute.Length)
            {
                float bend = Vector3.Angle(direction, dispatchRoute[routeIndex + 1] - dispatchRoute[routeIndex]);
                if (bend > 35f) speedLimit = Mathf.Min(speedLimit, Mathf.Lerp(4f, maxSpeed, Mathf.Clamp01((direction.magnitude - 4f) / 18f)));
            }
            if (clearingBay && routeIndex < 3)
                speedLimit = Mathf.Min(speedLimit, Mathf.Abs(angle) > 15f
                    ? (routeIndex < 2 ? 2.25f : 2.8f)
                    : Mathf.Lerp(2.25f, routeIndex < 2 ? 6f : 8f, Mathf.Clamp01((direction.magnitude - 4f) / 12f)));
            motor.Drive(throttle, Mathf.Clamp(turn, -1f, 1f), false, Tuning(speedLimit));
        }

        bool ClearRoute(Vector3 point)
        {
            Vector3 start = transform.position + Vector3.up * .9f;
            Vector3 delta = point + Vector3.up * .9f - start;
            foreach (var hit in Physics.SphereCastAll(start, .8f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody == rb) continue;
                var target = hit.collider.GetComponentInParent<ArcadeVehicleController>();
                if (target != null && target.HasDriver) continue;
                return false;
            }
            return true;
        }

        bool GroundAlongRoute(Vector3 point)
        {
            float distance = Vector3.Distance(transform.position, point);
            for (float step = 8f; step < distance; step += 8f)
            {
                Vector3 sample = Vector3.Lerp(transform.position, point, step / distance);
                bool supported = false;
                foreach (var hit in Physics.RaycastAll(sample + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                    supported |= hit.collider.attachedRigidbody == null && hit.point.y > -.5f && hit.normal.y > .7f;
                if (!supported) return false;
            }
            return true;
        }

        private VehicleGroundMotor.Tuning Tuning(float speedLimit)
        {
            return new VehicleGroundMotor.Tuning
            {
                acceleration = acceleration, reverseAcceleration = acceleration * .65f, steering = clearingBay && routeIndex < 3 ? 180f : turnSpeed,
                maxSpeed = speedLimit, grip = 6f, braking = 8f, alignment = 8f
            };
        }
    }
}
