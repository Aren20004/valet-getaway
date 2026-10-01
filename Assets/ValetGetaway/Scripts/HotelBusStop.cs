using UnityEngine;

namespace ValetGetaway
{
    public sealed class HotelBusStop : MonoBehaviour
    {
        [SerializeField] private float interactionRadius = 4f;
        public bool IsNearby(ThirdPersonPlayerController player) => !player.IsDriving &&
            Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude < interactionRadius;
    }
}
