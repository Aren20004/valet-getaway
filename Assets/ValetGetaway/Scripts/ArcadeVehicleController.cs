using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    [RequireComponent(typeof(Rigidbody), typeof(VehicleGroundMotor))]
    public sealed class ArcadeVehicleController : MonoBehaviour
    {
        [SerializeField] private float acceleration = 24f;
        [SerializeField] private float reverseAcceleration = 14f;
        [SerializeField] private float steering = 95f;
        [SerializeField] private float lateralGrip = 5f;
        [SerializeField] private float maxSpeed = 24f;
        [SerializeField] private float braking = 8f;
        [SerializeField] private float slopeAlignment = 8f;
        [Tooltip("Full lock per second: how fast the steering rack itself moves.")]
        [SerializeField, Min(.5f)] private float steerRate = 5f;
        [Tooltip("The most lateral grip the tyres hold, in m/s^2, before the car starts to slide wide.")]
        [SerializeField, Min(1f)] private float gripLimit = 13f;
        VehicleGroundMotor motor;
        float handbrake;
        public float LastThrottle { get; private set; }
        public bool Grounded => motor != null && motor.Grounded;
        public bool HasDriver { get; set; }
        public float Speed => motor == null ? 0f : motor.Speed;
        /// <summary>Sideways speed across the tyres, m/s (how hard the car is sliding).</summary>
        public float Slip => motor == null ? 0f : motor.Slip;
        public float Handbrake => handbrake;
        /// <summary>Scales acceleration and top speed; a damaged car limps home on this.</summary>
        public float PowerScale { get; set; } = 1f;
        /// <summary>Scales steering authority; drops with damage so a wrecked car wanders.</summary>
        public float SteeringScale { get; set; } = 1f;

        void Awake()
        {
            motor = GetComponent<VehicleGroundMotor>();
            if (motor == null) motor = gameObject.AddComponent<VehicleGroundMotor>();
        }

        void FixedUpdate()
        {
            var keyboard = HasDriver ? Keyboard.current : null;
            LastThrottle = keyboard == null ? 0f : (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float turn = keyboard == null ? 0f : (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            // Space is the handbrake, not a wall: it comes on over a moment and lets the back end come round.
            bool pulled = keyboard != null && keyboard.spaceKey.isPressed;
            handbrake = Mathf.MoveTowards(handbrake, pulled ? 1f : 0f, (pulled ? 7f : 5f) * Time.fixedDeltaTime);
            // S past a standstill is the brake pedal; the motor reads that from the throttle sign itself.
            motor.Drive(LastThrottle, turn, false, handbrake, new VehicleGroundMotor.Tuning
            {
                acceleration = acceleration * PowerScale, reverseAcceleration = reverseAcceleration * PowerScale,
                steering = steering * SteeringScale, grip = lateralGrip, maxSpeed = maxSpeed * PowerScale,
                braking = braking, alignment = slopeAlignment, steerRate = steerRate, gripLimit = gripLimit
            });
        }
    }
}
