using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonPlayerController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -25f;

        private CharacterController characterController;
        [SerializeField] private Transform cameraTransform;
        private Renderer[] renderers;
        private float verticalVelocity;

        public bool IsDriving { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void SetCamera(Transform cameraToUse)
        {
            cameraTransform = cameraToUse;
        }

        private void Update()
        {
            if (IsDriving || Keyboard.current == null || cameraTransform == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            Vector3 movement = forward * input.y + right * input.x;

            if (movement.sqrMagnitude > 0.01f)
            {
                float speed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
                characterController.Move(movement * speed * Time.deltaTime);
                Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        public void EnterVehicle(Transform seat)
        {
            IsDriving = true;
            characterController.enabled = false;
            SetVisible(false);
            transform.SetParent(seat);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void ExitVehicle(Vector3 exitPosition, Quaternion exitRotation)
        {
            transform.SetParent(null);
            transform.SetPositionAndRotation(exitPosition, exitRotation);
            SetVisible(true);
            characterController.enabled = true;
            verticalVelocity = 0f;
            IsDriving = false;
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer playerRenderer in renderers)
            {
                playerRenderer.enabled = visible;
            }
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (IsDriving) return;
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            verticalVelocity = 0f;
            characterController.enabled = true;
        }
    }
}
