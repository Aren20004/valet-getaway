
using UnityEngine;
using UnityEngine.AI;

namespace ValetGetaway
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PoliceFootAI : MonoBehaviour
    {
        public ThirdPersonPlayerController player;
        public float chaseDistance = 30f;
        public float stopDistance = 1.5f;

        private NavMeshAgent agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (player == null)
                return;

            // Stop chasing when the player is driving
            if (player.IsDriving)
            {
                agent.isStopped = true;
                return;
            }

            float distance = Vector3.Distance(
                transform.position,
                player.transform.position
            );

            if (distance <= chaseDistance)
            {
                agent.stoppingDistance = stopDistance;
                agent.isStopped = false;
                agent.SetDestination(player.transform.position);
            }
            else
            {
                agent.isStopped = true;
            }
        }
    }
}
