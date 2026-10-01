using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeVehicleController : MonoBehaviour
    {
        [SerializeField] private float acceleration = 24f;
        [SerializeField] private float reverseAcceleration = 14f;
        [SerializeField] private float steering = 95f;
        [SerializeField] private float lateralGrip = 5f;
        [SerializeField] private float maxSpeed = 24f;
        [SerializeField] private float braking = 8f;

        private Rigidbody body;

        public bool HasDriver { get; set; }
        public float Speed => body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.centerOfMass = new Vector3(0f, -0.55f, 0f);
        }

        private void FixedUpdate()
        {
            if (!HasDriver || Keyboard.current == null)
            {
                ApplyRollingResistance();
                return;
            }

            float throttle = 0f;
            if (Keyboard.current.wKey.isPressed) throttle += 1f;
            if (Keyboard.current.sKey.isPressed) throttle -= 1f;

            float steerInput = 0f;
            if (Keyboard.current.dKey.isPressed) steerInput += 1f;
            if (Keyboard.current.aKey.isPressed) steerInput -= 1f;

            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.2f);
            if (!grounded)
            {
                return;
            }

            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float power = throttle >= 0f ? acceleration : reverseAcceleration;
            if (Mathf.Abs(forwardSpeed) < maxSpeed || Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed))
            {
                body.AddForce(transform.forward * throttle * power, ForceMode.Acceleration);
            }

            float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2f);
            float direction = Mathf.Abs(forwardSpeed) < 0.25f ? 1f : Mathf.Sign(forwardSpeed);
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, steerInput * steering * speedFactor * direction * Time.fixedDeltaTime, 0f));

            Vector3 lateralVelocity = transform.right * Vector3.Dot(body.linearVelocity, transform.right);
            body.AddForce(-lateralVelocity * lateralGrip, ForceMode.Acceleration);

            if (Keyboard.current.spaceKey.isPressed)
            {
                body.AddForce(-body.linearVelocity * braking, ForceMode.Acceleration);
            }
            else if (Mathf.Approximately(throttle, 0f))
            {
                ApplyRollingResistance();
            }
        }

        private void ApplyRollingResistance()
        {
            if (body == null)
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            body.AddForce(-horizontalVelocity * 0.45f, ForceMode.Acceleration);
        }
    }
}
