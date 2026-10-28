using System.Collections.Generic;
using UnityEngine;

namespace ValetGetaway
{
    /// <summary>
    /// What turns a police car from a tail into a threat. Once it is alongside the player's rear quarter and moving,
    /// it aims at that corner rather than at the player, which is the PIT manoeuvre: the nudge spins the stolen car.
    /// Every contact damages the car (feeding limp mode) and restarts the clock on the helicopter.
    ///
    /// Added automatically by <see cref="PoliceCarAI"/>, so every cruiser in the fleet can do it.
    /// </summary>
    [RequireComponent(typeof(PoliceCarAI))]
    [DisallowMultipleComponent]
    public sealed class PoliceRam : MonoBehaviour
    {
        [Tooltip("How close the cruiser must be before it tries a PIT rather than just following.")]
        [SerializeField, Min(3f)] float pitRange = 11f;
        [Tooltip("Both cars must be doing at least this, m/s, for a PIT to be attempted.")]
        [SerializeField, Min(0f)] float pitSpeed = 7f;
        [Tooltip("Seconds between attempts, so a cruiser does not grind along the player's flank.")]
        [SerializeField, Min(.5f)] float cooldown = 3.5f;
        [Tooltip("Yaw impulse put into the player's car on a clean rear-quarter hit.")]
        [SerializeField, Min(0f)] float spin = 2.6f;

        static readonly List<PoliceRam> active = new();
        /// <summary>Every cruiser in the scene that can ram, for the chase to count who is alongside.</summary>
        public static IReadOnlyList<PoliceRam> Active => active;

        PoliceCarAI ai;
        Rigidbody body;
        float nextAttempt;
        int side;

        void Awake()
        {
            ai = GetComponent<PoliceCarAI>();
            body = GetComponent<Rigidbody>();
            side = Random.value < .5f ? -1 : 1;
        }

        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); }

        void FixedUpdate()
        {
            var chase = PoliceChase.Current;
            if (chase == null || !chase.Active || ai == null || !ai.PursuitEnabled || ai.HasDispatchRoute) return;
            var target = chase.PlayerCar;
            if (target == null) return;

            Vector3 toTarget = target.transform.position - transform.position;
            float distance = toTarget.magnitude;

            // With the helicopter overhead the cars are told where to look, so they stop guessing and close in.
            if (chase.Spotted && distance > pitRange * 3f)
            {
                ai.TargetOffset = Vector3.zero;
                return;
            }

            float closing = body != null ? body.linearVelocity.magnitude : 0f;
            bool behind = Vector3.Dot(toTarget.normalized, target.transform.forward) > .35f;
            if (distance > pitRange || closing < pitSpeed || !behind || Time.time < nextAttempt)
            {
                // Sit on one shoulder rather than directly in the player's tyre tracks.
                ai.TargetOffset = target.transform.right * (side * 2.2f);
                return;
            }

            // Aim past the player's rear corner: the cruiser's nose ends up on the quarter panel, and the back of
            // the stolen car steps out.
            ai.TargetOffset = target.transform.right * (side * 1.5f) - target.transform.forward * 1.6f;
        }

        void OnCollisionEnter(Collision collision)
        {
            var chase = PoliceChase.Current;
            if (chase == null || !chase.Active || chase.PlayerBody == null) return;
            if (collision.rigidbody != chase.PlayerBody) return;

            nextAttempt = Time.time + cooldown;
            chase.ReportContact();

            float impact = collision.relativeVelocity.magnitude;
            chase.PlayerCar.ApplyImpact(impact);

            // A hit on the rear quarter spins the car; a hit anywhere else just shoves it.
            Vector3 contact = collision.GetContact(0).point;
            Vector3 local = chase.PlayerCar.transform.InverseTransformPoint(contact);
            if (local.z < -.4f && Mathf.Abs(local.x) > .3f && impact > 4f)
            {
                float push = Mathf.Clamp(impact / 12f, .3f, 1.4f) * spin * Mathf.Sign(local.x);
                chase.PlayerBody.AddTorque(Vector3.up * (push * chase.PlayerBody.mass), ForceMode.Impulse);
            }
        }
    }
}
