using UnityEngine;
using UnityEngine.AI;

namespace ValetGetaway
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class BuyerWander : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float stopDistance = 8f;
        [SerializeField] private float resumeDistance = 11f;
        private NavMeshAgent agent;
        private Transform observer;
        private BuyerDeliveryManager delivery;
        private bool stoppedForPlayer;
        private float nextDestinationTime;

        public void Initialize(Transform player, BuyerDeliveryManager deliveryManager)
        {
            observer = player;
            delivery = deliveryManager;
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.baseOffset = 1f;
            agent.angularSpeed = 180f;
            agent.stoppingDistance = 0.4f;
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position + Vector3.up * agent.baseOffset);
        }

        private void Update()
        {
            if (agent == null || observer == null || !agent.isOnNavMesh) return;
            Vector3 direction = observer.position - transform.position;
            direction.y = 0f;
            float range = stoppedForPlayer ? resumeDistance : stopDistance;
            bool parkedForSale = delivery != null && delivery.IsWaitingAtParkedCar(transform);
            stoppedForPlayer = parkedForSale || direction.magnitude < range && BuyerDeliveryManager.ClearSight(transform.position + Vector3.up * 0.5f, observer.position + Vector3.up, transform, observer);
            agent.isStopped = stoppedForPlayer;
            agent.updateRotation = !stoppedForPlayer;
            if (stoppedForPlayer)
            {
                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240f * Time.deltaTime);
                return;
            }
            if (agent.pathPending || agent.hasPath && agent.remainingDistance > 0.8f || Time.time < nextDestinationTime) return;
            nextDestinationTime = Time.time + 2f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * 28f;
                Vector3 candidate = transform.position + new Vector3(offset.x, 0, offset.y);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas)) continue;
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.SetPath(path);
                    break;
                }
            }
        }
    }
}
