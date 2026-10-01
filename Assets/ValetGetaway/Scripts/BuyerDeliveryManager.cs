using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace ValetGetaway
{
    public sealed class BuyerDeliveryManager : MonoBehaviour
    {
        [SerializeField] private float deliveryRadius = 4.5f;
        [SerializeField] private float maximumDeliverySpeed = 1f;
        [SerializeField] private float talkingDistance = 2.5f;
        [SerializeField] private float discoveryDistance = 24f;
        private Material glowMaterial;
        private HotelGameSession session;
        [SerializeField] private NavMeshSurface navigationSurface;
        public Transform[] Buyers => buyers;
        public void SetNavigationSurface(NavMeshSurface surface) => navigationSurface=surface;
        public VehicleInteraction AssignedCar => assignedCar;
        public Transform VisibleBuyer => HasDelivery && BuyerDiscovered ? buyers[selectedBuyer] : null;
        public bool BuyerDiscovered { get; private set; }

        private void Start()
        {
            session = Object.FindAnyObjectByType<HotelGameSession>();
            marker.SetActive(false);
            parkingBoundary.gameObject.SetActive(false);
            glowMaterial = new Material(highlightMaterial);
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor("_EmissionColor", new Color(1f, 0.65f, 0.05f) * 2f);
            var surface=navigationSurface;
            if(surface==null)
            {
                Transform area=transform.parent.Find("Test Area");
                surface=area.GetComponent<NavMeshSurface>();
                if(surface==null) surface=area.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects=CollectObjects.Children;
                surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            }
            if(surface.navMeshData==null) surface.BuildNavMesh();
            foreach (Transform buyer in buyers)
            {
                BuyerWander wander = buyer.GetComponent<BuyerWander>();
                if (wander == null) wander = buyer.gameObject.AddComponent<BuyerWander>();
                wander.Initialize(observer, this);
            }
        }

        private void Update()
        {
            if (!HasDelivery || viewCamera == null) return;
            Transform buyer = buyers[selectedBuyer];
            if (!BuyerDiscovered)
            {
                Vector3 viewport = viewCamera.WorldToViewportPoint(buyer.position);
                BuyerDiscovered = Vector3.Distance(observer.position, buyer.position) <= discoveryDistance &&
                    viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1 &&
                    ClearSight(viewCamera.transform.position, buyer.position + Vector3.up * 0.5f, observer, buyer);
            }
            buyerRenderers[selectedBuyer].sharedMaterial = BuyerDiscovered ? glowMaterial : idleMaterial;
            marker.SetActive(BuyerDiscovered);
            parkingBoundary.gameObject.SetActive(BuyerDiscovered && NearBuyer);
            if (!BuyerDiscovered) return;
            marker.transform.position = buyer.position + Vector3.up * 4f;
            for (int i = 0; i < parkingBoundary.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / parkingBoundary.positionCount;
                parkingBoundary.SetPosition(i, buyer.position + new Vector3(Mathf.Cos(angle) * deliveryRadius, -0.96f, Mathf.Sin(angle) * deliveryRadius));
            }
        }

        public static bool ClearSight(Vector3 from, Vector3 to, Transform observerRoot, Transform targetRoot)
        {
            Vector3 delta = to - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform obstacle = hit.collider.transform;
                if (obstacle.IsChildOf(observerRoot) || obstacle.IsChildOf(targetRoot)) continue;
                if (observerRoot.IsChildOf(obstacle) || targetRoot.IsChildOf(obstacle)) continue;
                return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            if (glowMaterial != null) Destroy(glowMaterial);
        }

        private readonly Vector3[] buyerLocations =
        {
            new Vector3(55f, 0f, -40f),
            new Vector3(-55f, 0f, -40f),
            new Vector3(55f, 0f, 40f),
            new Vector3(15f, 0f, 40f),
            new Vector3(-55f, 0f, 40f),
            new Vector3(40f, 0f, 12f)
        };

        [SerializeField] private Transform[] buyers;
        [SerializeField] private Renderer[] buyerRenderers;
        [SerializeField] private Material idleMaterial;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private GameObject marker;
        [SerializeField] private LineRenderer parkingBoundary;
        private VehicleInteraction assignedCar;
        private Rigidbody carBody;
        [SerializeField] private Transform observer;
        [SerializeField] private Camera viewCamera;
        private int selectedBuyer = -1;
        private int previousBuyer = -1;

        public bool HasDelivery => selectedBuyer >= 0;
        public int DeliveriesCompleted { get; private set; }
        public bool NearBuyer => HasDelivery && assignedCar != null &&
            Vector3.ProjectOnPlane(assignedCar.transform.position - buyers[selectedBuyer].position, Vector3.up).magnitude <= deliveryRadius;
        public bool CanDeliver => NearBuyer && carBody != null && carBody.linearVelocity.magnitude <= maximumDeliverySpeed;
        public bool IsWaitingAtParkedCar(Transform buyer) => HasDelivery && buyers[selectedBuyer] == buyer && CanDeliver;
        public bool CanTalk => HasDelivery && BuyerDiscovered && observer != null &&
            Vector3.ProjectOnPlane(observer.position - buyers[selectedBuyer].position, Vector3.up).magnitude <= talkingDistance &&
            ClearSight(observer.position + Vector3.up, buyers[selectedBuyer].position, observer, buyers[selectedBuyer]);
        public string Objective => HasDelivery
            ? !BuyerDiscovered ? "Find a buyer for the car" : CanDeliver ? "Car parked. Get out and talk to the buyer." : "Park near the buyer, then talk to them"
            : DeliveriesCompleted > 0 ? "Delivered! Take the car at its starting spot for another job." : "Take a car to find a buyer";

        public void Initialize(Transform player, Camera cameraToUse, Material idle, Material highlight)
        {
            observer = player;
            viewCamera = cameraToUse;
            idleMaterial = idle;
            highlightMaterial = highlight;
            buyers = new Transform[buyerLocations.Length];
            buyerRenderers = new Renderer[buyerLocations.Length];
            for (int i = 0; i < buyers.Length; i++)
            {
                GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npc.name = $"Buyer NPC {i + 1}";
                npc.transform.SetParent(transform);
                npc.transform.position = buyerLocations[i] + Vector3.up;
                buyers[i] = npc.transform;
                buyerRenderers[i] = npc.GetComponent<Renderer>();
                buyerRenderers[i].sharedMaterial = idleMaterial;
            }

            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Active Buyer Beacon";
            marker.transform.SetParent(transform);
            marker.transform.localScale = new Vector3(0.18f, 2f, 0.18f);
            marker.GetComponent<Collider>().enabled = false;
            if (Application.isPlaying) Destroy(marker.GetComponent<Collider>());
            else DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = highlightMaterial;
            marker.SetActive(false);

            GameObject boundaryObject = new GameObject("Buyer Parking Boundary", typeof(LineRenderer));
            boundaryObject.transform.SetParent(transform);
            parkingBoundary = boundaryObject.GetComponent<LineRenderer>();
            parkingBoundary.sharedMaterial = highlightMaterial;
            parkingBoundary.useWorldSpace = true;
            parkingBoundary.loop = true;
            parkingBoundary.widthMultiplier = 0.12f;
            parkingBoundary.positionCount = 64;
            boundaryObject.SetActive(false);
        }

        public void AssignBuyer(VehicleInteraction car)
        {
            if (HasDelivery || buyers == null || buyers.Length == 0) return;

            // Exclude the previous buyer so consecutive jobs have different destinations.
            int choice = Random.Range(0, buyers.Length - (previousBuyer >= 0 ? 1 : 0));
            if (previousBuyer >= 0 && choice >= previousBuyer) choice++;
            selectedBuyer = choice;
            assignedCar = car;
            carBody = car.GetComponent<Rigidbody>();
            BuyerDiscovered = false;
            marker.SetActive(false);
            parkingBoundary.gameObject.SetActive(false);
        }

        public bool TrySell(VehicleInteraction car)
        {
            if (car != assignedCar || !CanDeliver || !CanTalk || observer.GetComponent<ThirdPersonPlayerController>().IsDriving) return false;

            car.CompleteDelivery();
            buyerRenderers[selectedBuyer].sharedMaterial = idleMaterial;
            previousBuyer = selectedBuyer;
            selectedBuyer = -1;
            BuyerDiscovered = false;
            assignedCar = null;
            carBody = null;
            marker.SetActive(false);
            parkingBoundary.gameObject.SetActive(false);
            DeliveriesCompleted++;
            if (session != null) session.PayForSale(car);
            else StartCoroutine(ResetCar(car));
            return true;
        }

        private IEnumerator ResetCar(VehicleInteraction car)
        {
            yield return new WaitForSeconds(2f);
            if (car != null) car.ResetAfterDelivery();
        }

        private void OnGUI()
        {
            if (session != null) return;
            if (observer == null || viewCamera == null) return;
            GUIStyle label = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            float width = Mathf.Min(480f, Screen.width - 24f);
            GUI.Box(new Rect(12f, 12f, width, 64f), $"{Objective}\nDeliveries: {DeliveriesCompleted}", label);
            if (!HasDelivery || !BuyerDiscovered) return;

            Vector3 point = viewCamera.WorldToScreenPoint(buyers[selectedBuyer].position + Vector3.up * 2f);
            float distance = Vector3.Distance(observer.position, buyers[selectedBuyer].position);
            if (point.z > 0 && point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height)
            {
                float markerWidth = Mathf.Min(160f, Screen.width - 24f);
                GUI.Box(new Rect(Mathf.Clamp(point.x - markerWidth / 2f, 12f, Screen.width - markerWidth - 12f),
                    Mathf.Clamp(Screen.height - point.y - 36f, 84f, Mathf.Max(84f, Screen.height - 120f)), markerWidth, 36f),
                    $"BUYER  {distance:0} m", label);
            }
            else
            {
                Vector3 local = viewCamera.transform.InverseTransformPoint(buyers[selectedBuyer].position);
                string direction = Mathf.Abs(local.x) < 1f && local.z < 0 ? "Behind you" : local.x < 0 ? "Look left" : "Look right";
                GUI.Box(new Rect(12f, 84f, width, 36f), $"BUYER  {distance:0} m  -  {direction}", label);
            }
        }
    }
}
