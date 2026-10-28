
using UnityEngine;
using UnityEngine.AI;

namespace ValetGetaway
{
    public class PoliceVehicleController : MonoBehaviour
    {
        [Header("Player")]
        public ThirdPersonPlayerController player;

        [Header("Assigned Police Unit")]
        public GameObject policeOfficer;
        public PoliceCarAI policeCarAI;
        public Transform driverSeat;
        public Transform vehicleAccessPoint;

        [Header("Entering Vehicle")]
        public float enterDistance = 1.5f;
        public float walkSpeed = 3.5f;
        public float navMeshSearchRadius = 3f;

        private NavMeshAgent agent;
        private PoliceFootAI footAI;

        private bool isInCar = false;
        private bool goingToCar = false;

        private float originalSpeed;
        private float originalStoppingDistance;

        private void Awake()
        {
            if (policeOfficer != null)
            {
                agent = policeOfficer.GetComponent<NavMeshAgent>();
                footAI = policeOfficer.GetComponent<PoliceFootAI>();
            }

            if (agent != null)
            {
                originalSpeed = agent.speed;
                originalStoppingDistance = agent.stoppingDistance;
            }

            if (policeCarAI != null)
            {
                policeCarAI.enabled = false;
            }
        }

        private void Update()
        {
            if (player == null ||
                policeOfficer == null ||
                policeCarAI == null ||
                driverSeat == null ||
                vehicleAccessPoint == null)
            {
                return;
            }

            if (player.IsDriving)
            {
                if (!isInCar)
                {
                    MoveToPoliceCar();
                }
            }
            else
            {
                if (isInCar)
                {
                    ExitPoliceCar();
                }
                else if (goingToCar)
                {
                    CancelEntering();
                }
            }
        }

        private void MoveToPoliceCar()
        {
            if (agent == null)
            {
                Debug.LogWarning(
                    "Police officer has no NavMeshAgent!"
                );
                return;
            }

            if (!agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning(
                    "Police officer is not on the NavMesh!"
                );
                return;
            }

            if (!goingToCar)
            {
                goingToCar = true;

                // Stop chasing the player
                if (footAI != null)
                {
                    footAI.enabled = false;
                }

                // Start walking toward the car
                agent.isStopped = false;
                agent.speed = walkSpeed;
                agent.stoppingDistance = 0.2f;
                agent.ResetPath();

                Debug.Log(
                    "Police officer is walking to the car!"
                );
            }

            // Find walkable ground near the car door
            if (!NavMesh.SamplePosition(
                vehicleAccessPoint.position,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
            {
                Debug.LogWarning(
                    "No NavMesh near VehicleAccessPoint!"
                );
                return;
            }

            // Check whether a complete route exists
            NavMeshPath path = new NavMeshPath();

            if (!agent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                Debug.LogWarning(
                    "Police officer cannot reach VehicleAccessPoint!"
                );
                return;
            }

            // Walk toward the car
            agent.isStopped = false;
            agent.SetDestination(hit.position);

            // Measure horizontal distance to the access point
            Vector3 difference =
                policeOfficer.transform.position -
                vehicleAccessPoint.position;

            difference.y = 0f;

            float distance = difference.magnitude;

            // Enter only when close enough
            if (distance <= enterDistance &&
                !agent.pathPending)
            {
                EnterPoliceCar();
            }
        }

        private void EnterPoliceCar()
        {
            goingToCar = false;
            isInCar = true;

            // Stop NavMesh movement
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }

                agent.enabled = false;
            }

            // Move officer into driver's seat
            policeOfficer.transform.SetParent(driverSeat);

            policeOfficer.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity
            );

            policeOfficer.SetActive(false);

            // Start police car chase
            var carBody = policeCarAI.GetComponent<Rigidbody>();
            if (carBody != null)
            {
                carBody.isKinematic = false;
                carBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            policeCarAI.enabled = true;

            Debug.Log("Police officer entered car!");
        }

        private void CancelEntering()
        {
            goingToCar = false;

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;

                agent.speed = originalSpeed;
                agent.stoppingDistance =
                    originalStoppingDistance;
            }

            if (footAI != null)
            {
                footAI.enabled = true;
            }

            Debug.Log(
                "Police officer stopped going to the car."
            );
        }

        private void ExitPoliceCar()
        {
            isInCar = false;
            goingToCar = false;

            // Stop police car AI
            policeCarAI.enabled = false;

            Vector3 exitPosition =
                vehicleAccessPoint.position;

            Quaternion exitRotation =
                vehicleAccessPoint.rotation;

            // Find nearby walkable ground
            bool foundNavMesh = NavMesh.SamplePosition(
                exitPosition,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas
            );

            if (foundNavMesh)
            {
                exitPosition = hit.position;
            }
            else
            {
                Debug.LogWarning(
                    "No NavMesh found near VehicleAccessPoint!"
                );
            }

            // Remove officer from the car
            policeOfficer.transform.SetParent(transform, true);

            policeOfficer.transform.SetPositionAndRotation(
                exitPosition,
                exitRotation
            );

            policeOfficer.SetActive(true);

            // Restore NavMeshAgent
            if (agent != null)
            {
                agent.speed = originalSpeed;
                agent.stoppingDistance =
                    originalStoppingDistance;

                if (foundNavMesh)
                {
                    agent.enabled = true;

                    if (agent.isOnNavMesh)
                    {
                        agent.Warp(exitPosition);
                        agent.isStopped = false;
                    }
                }
            }

            // Resume foot chase
            if (footAI != null)
            {
                footAI.enabled = true;
            }

            Debug.Log("Police officer exited car!");
        }
    }
}
