using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    public sealed class OrbitFollowCamera : MonoBehaviour
    {
        [SerializeField] private float distance = 6f;
        [SerializeField] private float height = 2f;
        [SerializeField] private float mouseSensitivity = 0.14f;
        [SerializeField] private float followSharpness = 14f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 65f;

        [SerializeField] private Transform target;
        private bool followTargetHeading;
        private float yaw;
        private float pitch = 18f;

        public void SetTarget(Transform newTarget, bool snap = false, bool followHeading = false)
        {
            float worldYaw = yaw + (followTargetHeading && target != null ? target.eulerAngles.y : 0f);
            target = newTarget;
            followTargetHeading = followHeading;
            // In driving mode, mouse yaw is an offset relative to the car's heading.
            yaw = worldYaw - (followTargetHeading && target != null ? target.eulerAngles.y : 0f);
            if (snap && target != null)
            {
                yaw = followTargetHeading ? 0f : target.eulerAngles.y;
                UpdateCamera(1f);
            }
        }

        private void Start()
        {
            if (target != null) SetTarget(target, true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                bool shouldLock = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !shouldLock;
            }

            if (target == null)
            {
                return;
            }

            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = Mouse.current.delta.ReadValue();
                yaw += look.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
            }

            UpdateCamera(Time.deltaTime);
        }

        private void UpdateCamera(float deltaTime)
        {
            float heading = yaw + (followTargetHeading ? target.eulerAngles.y : 0f);
            Quaternion orbit = Quaternion.Euler(pitch, heading, 0f);
            Vector3 focusPoint = target.position + Vector3.up * height;
            Vector3 desiredPosition = focusPoint - orbit * Vector3.forward * distance;
            float blend = 1f - Mathf.Exp(-followSharpness * deltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
        }
    }
}
