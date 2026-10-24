
using UnityEngine;

namespace ValetGetaway
{
    [RequireComponent(typeof(Rigidbody))]
    public class PoliceCarAI : MonoBehaviour
    {
        public ThirdPersonPlayerController player;
        public float acceleration = 18f;
        public float turnSpeed = 100f;
        public float maxSpeed = 20f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (player == null || !player.IsDriving)
                return;

            Vector3 direction =
                player.transform.position - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            rb.MoveRotation(Quaternion.RotateTowards(
                rb.rotation,
                targetRotation,
                turnSpeed * Time.fixedDeltaTime
            ));

            if (rb.linearVelocity.magnitude < maxSpeed)
            {
                rb.AddForce(
                    transform.forward * acceleration,
                    ForceMode.Acceleration
                );
            }
        }
    }
}
