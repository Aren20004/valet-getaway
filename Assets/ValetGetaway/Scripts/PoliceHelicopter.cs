using UnityEngine;

namespace ValetGetaway
{
    /// <summary>
    /// The precinct's air unit. It sits on the helipad until a chase has run for half a minute without a cruiser
    /// laying a hand on the player; then it lifts off, flies to the player's last known position, sweeps for them
    /// and, once it has them in the light, calls them in so every car on the ground drives straight at them.
    /// Losing the light (going under cover, or a cruiser making contact) sends it back to searching.
    ///
    /// Wired by Valet Getaway > Police Station > Set Up Helicopter Chase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceHelicopter : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField, Min(10f)] float cruiseHeight = 38f;
        [SerializeField, Min(5f)] float cruiseSpeed = 34f;
        [SerializeField, Min(1f)] float climbSpeed = 9f;
        [Tooltip("How far out it circles while it holds station over the player.")]
        [SerializeField, Min(4f)] float orbitRadius = 17f;
        [SerializeField] float orbitDegreesPerSecond = 26f;

        [Header("Search")]
        [Tooltip("The light has to be roughly on the player, within this many metres, to count as a lock.")]
        [SerializeField, Min(5f)] float lockRadius = 22f;
        [Tooltip("Seconds of holding the player in the light before the ground units are told.")]
        [SerializeField, Min(0f)] float lockTime = 1.2f;
        [Tooltip("Metres the search pattern wanders while it has not found the player.")]
        [SerializeField, Min(0f)] float searchSweep = 30f;

        [Header("Parts")]
        [SerializeField] Transform mainRotor;
        [SerializeField] Transform tailRotor;
        [SerializeField] Light searchLight;

        enum Mode { Parked, Climbing, Transit, Hunting, Returning }

        HotelGameSession session;
        Mode mode = Mode.Parked;
        Vector3 padPosition;
        Quaternion padRotation;
        Vector3 aim;          // where the light is pointed, on the ground
        float orbit, held, rotorSpin;

        public bool Airborne => mode != Mode.Parked;

        void Awake()
        {
            session = Object.FindAnyObjectByType<HotelGameSession>();
            padPosition = transform.position;
            padRotation = transform.rotation;
            aim = padPosition;
            if (searchLight != null) searchLight.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var chase = PoliceChase.Current;
            bool wanted = chase != null && chase.AirSupportCalled;

            Spin(dt);

            switch (mode)
            {
                case Mode.Parked:
                    if (wanted) { mode = Mode.Climbing; held = 0f; if (searchLight != null) searchLight.enabled = true; }
                    break;

                case Mode.Climbing:
                    if (!wanted) { mode = Mode.Returning; break; }
                    transform.position = Vector3.MoveTowards(transform.position,
                        new Vector3(padPosition.x, padPosition.y + cruiseHeight, padPosition.z), climbSpeed * dt);
                    if (transform.position.y >= padPosition.y + cruiseHeight - .3f) mode = Mode.Transit;
                    break;

                case Mode.Transit:
                case Mode.Hunting:
                    if (!wanted) { mode = Mode.Returning; if (chase != null) chase.Spotted = false; break; }
                    Hunt(chase, dt);
                    break;

                case Mode.Returning:
                    if (wanted) { mode = Mode.Transit; break; }
                    if (chase != null) chase.Spotted = false;
                    Return(dt);
                    break;
            }

            PointLight(dt);
        }

        void Hunt(PoliceChase chase, float dt)
        {
            Transform player = chase.PlayerCar != null ? chase.PlayerCar.transform : null;
            if (player == null) { mode = Mode.Returning; return; }

            Vector3 centre = player.position;
            float flat = Vector3.ProjectOnPlane(centre - transform.position, Vector3.up).magnitude;

            if (mode == Mode.Transit)
            {
                Fly(new Vector3(centre.x, padPosition.y + cruiseHeight, centre.z), dt);
                if (flat < orbitRadius * 1.6f) { mode = Mode.Hunting; orbit = Random.Range(0f, 360f); }
            }
            else
            {
                orbit += orbitDegreesPerSecond * dt;
                var offset = Quaternion.Euler(0f, orbit, 0f) * Vector3.forward * orbitRadius;
                Fly(new Vector3(centre.x, padPosition.y + cruiseHeight, centre.z) + offset, dt);
            }

            // The light sweeps towards the player and only settles on them once it is close enough to count.
            Vector3 want = mode == Mode.Hunting && held > 0f
                ? centre
                : centre + new Vector3(Mathf.Sin(Time.time * .9f), 0f, Mathf.Cos(Time.time * .7f)) * searchSweep;
            aim = Vector3.Lerp(aim, want, Mathf.Clamp01(1.6f * dt));

            bool onTarget = Vector3.ProjectOnPlane(aim - centre, Vector3.up).magnitude < lockRadius && flat < orbitRadius * 2.5f;
            held = onTarget ? held + dt : 0f;

            bool locked = held >= lockTime;
            if (locked != chase.Spotted)
            {
                chase.Spotted = locked;
                if (locked && session != null) session.Notify("Spotlight on you! Every unit is being sent to your position.");
            }
            if (locked) chase.SpottedPosition = centre;
        }

        void Return(float dt)
        {
            Vector3 above = new(padPosition.x, padPosition.y + cruiseHeight, padPosition.z);
            if (Vector3.ProjectOnPlane(transform.position - above, Vector3.up).magnitude > 1.5f) { Fly(above, dt); return; }
            transform.position = Vector3.MoveTowards(transform.position, padPosition, climbSpeed * dt);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, padRotation, 60f * dt);
            if (Vector3.Distance(transform.position, padPosition) < .05f)
            {
                mode = Mode.Parked;
                transform.SetPositionAndRotation(padPosition, padRotation);
                if (searchLight != null) searchLight.enabled = false;
                aim = padPosition;
                held = 0f;
            }
        }

        void Fly(Vector3 to, float dt)
        {
            Vector3 step = Vector3.MoveTowards(transform.position, to, cruiseSpeed * dt);
            Vector3 travel = step - transform.position;
            transform.position = step;
            Vector3 heading = Vector3.ProjectOnPlane(travel, Vector3.up);
            if (heading.sqrMagnitude < .0004f) return;
            // Nose into the flight path and lean into the turn, the way a helicopter does.
            Quaternion want = Quaternion.LookRotation(heading.normalized, Vector3.up);
            float bank = Mathf.Clamp(Vector3.SignedAngle(transform.forward, heading.normalized, Vector3.up), -18f, 18f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want * Quaternion.Euler(6f, 0f, -bank), 90f * dt);
        }

        void Spin(float dt)
        {
            rotorSpin = Mathf.MoveTowards(rotorSpin, Airborne ? 1f : 0f, dt * .6f);
            if (mainRotor != null) mainRotor.Rotate(Vector3.up, 1500f * rotorSpin * dt, Space.Self);
            if (tailRotor != null) tailRotor.Rotate(Vector3.right, 2100f * rotorSpin * dt, Space.Self);
        }

        void PointLight(float dt)
        {
            if (searchLight == null || !searchLight.enabled) return;
            Vector3 down = aim - searchLight.transform.position;
            if (down.sqrMagnitude < .01f) return;
            searchLight.transform.rotation = Quaternion.RotateTowards(
                searchLight.transform.rotation, Quaternion.LookRotation(down.normalized, Vector3.up), 150f * dt);
        }
    }
}
