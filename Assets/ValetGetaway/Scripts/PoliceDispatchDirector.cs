using System.Collections.Generic;
using UnityEngine;

namespace ValetGetaway
{
    public sealed class PoliceDispatchDirector : MonoBehaviour
    {
        [SerializeField] HotelGameSession session;
        [SerializeField] PoliceVehicleController stationPatrol;
        [SerializeField] PoliceCarAI[] responseCars;
        [SerializeField] Transform[] aislePoints;
        [SerializeField] Transform[] stationExit;
        [SerializeField] Transform[] bridgeRoute;
        [SerializeField, Range(5, 10)] int minimumWave = 5;
        [SerializeField, Range(5, 10)] int maximumWave = 10;
        [SerializeField] Vector2 dispatchSpacing = new Vector2(.8f, 1.8f);
        [SerializeField] Vector2 chaseDuration = new Vector2(6f, 12f);
        [SerializeField] Vector2 regroupDuration = new Vector2(1.5f, 3.5f);

        enum ResponseState { Parked, Queued, Departing, Pursuing, Regrouping }
        sealed class Response
        {
            public PoliceCarAI ai;
            public Rigidbody body;
            public Pose home;
            public ResponseState state;
            public float nextChange;
        }
        Response[] fleet;
        VehicleInteraction targetCar;
        Pose patrolHome;
        public int WaveNumber { get; private set; }
        public int WaveSize { get; private set; }
        public int LaunchedThisWave { get; private set; }
        public int PursuitBursts { get; private set; }
        public int RegroupCount { get; private set; }
        public int DepartedCount { get; private set; }
        public int FleetSize => responseCars == null ? 0 : responseCars.Length + 1;
        public int PendingCount
        {
            get { int n = 0; if (fleet != null) foreach (var car in fleet) if (car.state == ResponseState.Queued) n++; return n; }
        }

        public void Configure(HotelGameSession game, PoliceVehicleController patrol, PoliceCarAI[] cars, Transform[] aisles, Transform[] exit, Transform[] bridge)
        {
            session = game; stationPatrol = patrol; responseCars = cars; aislePoints = aisles; stationExit = exit; bridgeRoute = bridge;
        }

        void Awake()
        {
            if (session != null) PoliceChase.Ensure(session);
            patrolHome = new Pose(stationPatrol.policeCarAI.transform.position, stationPatrol.policeCarAI.transform.rotation);
            fleet = new Response[responseCars.Length];
            for (int i = 0; i < fleet.Length; i++)
            {
                var ai = responseCars[i];
                fleet[i] = new Response { ai = ai, body = ai.GetComponent<Rigidbody>(), home = new Pose(ai.transform.position, ai.transform.rotation) };
                ai.player = session.Player;
                ai.PursuitEnabled = false;
                ai.enabled = false;
                fleet[i].body.isKinematic = true;
            }
        }

        void OnEnable() { if (session != null) session.CarTaken += Dispatch; }
        void OnDisable()
        {
            if (session != null) session.CarTaken -= Dispatch;
            StopWave();
        }

        void Dispatch(VehicleInteraction car)
        {
            if (car == null || session == null || !session.Player.IsDriving) return;
            StopWave();
            targetCar = car;
            WaveNumber++;
            int capacity = fleet.Length + 1;
            int lower = Mathf.Clamp(minimumWave, 1, capacity);
            int upper = Mathf.Clamp(maximumWave, lower, capacity);
            WaveSize = Random.Range(lower, upper + 1);
            LaunchedThisWave = 0;
            PursuitBursts = 0; RegroupCount = 0; DepartedCount = 0;
            var lead = stationPatrol.policeCarAI;
            if (Vector3.Distance(lead.transform.position, patrolHome.position) < 6f)
            {
                var departure = new List<Vector3>();
                var leadAisle = transform.Find("Dispatch route points/Patrol aisle entry");
                if (leadAisle != null && leadAisle.Find("Bay clearance") != null) departure.Add(leadAisle.Find("Bay clearance").position);
                departure.Add(leadAisle != null ? leadAisle.position : new Vector3(537f, lead.transform.position.y, lead.transform.position.z));
                foreach (var point in stationExit) departure.Add(point.position);
                lead.SetDispatchRoute(departure.ToArray(), leadAisle != null);
            }
            var order = new int[fleet.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                int saved = order[i]; order[i] = order[other]; order[other] = saved;
            }
            float when = Time.time + Random.Range(.8f, 2f);
            for (int i = 0; i < WaveSize - 1; i++)
            {
                var responder = fleet[order[i]];
                responder.state = ResponseState.Queued;
                responder.nextChange = when;
                when += Random.Range(dispatchSpacing.x, dispatchSpacing.y);
            }
            session.Notify("Police dispatched: " + WaveSize + " cars responding.");
        }

        void StopWave()
        {
            targetCar = null;
            if (fleet == null) return;
            foreach (var response in fleet)
            {
                response.state = ResponseState.Parked;
                response.ai.PursuitEnabled = false;
                response.ai.SetDispatchRoute(null);
            }
        }

        void Launch(Response response, int index)
        {
            response.body.isKinematic = false;
            response.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            response.ai.enabled = true;
            response.ai.acceleration = Random.Range(18f, 23f);
            response.ai.maxSpeed = Random.Range(21f, 26f);
            response.ai.turnSpeed = Random.Range(90f, 110f);
            response.ai.PursuitEnabled = true;
            if (Vector3.Distance(response.ai.transform.position, response.home.position) < 6f)
            {
                var route = new List<Vector3>();
                var clearance = aislePoints[index].Find("Bay clearance");
                if (clearance != null) route.Add(clearance.position);
                route.Add(aislePoints[index].position);
                foreach (var point in stationExit) route.Add(point.position);
                response.ai.SetDispatchRoute(route.ToArray(), clearance != null);
                response.state = ResponseState.Departing;
            }
            else BeginPursuit(response);
            LaunchedThisWave++;
        }

        void BeginPursuit(Response response)
        {
            if (response.state == ResponseState.Departing) DepartedCount++;
            response.state = ResponseState.Pursuing;
            response.ai.PursuitEnabled = true;
            float side = Random.value < .55f ? 0f : Random.Range(3f, 7f) * (Random.value < .5f ? -1f : 1f);
            response.ai.TargetOffset = session.Player.transform.right * side;
            response.nextChange = Time.time + Random.Range(chaseDuration.x, chaseDuration.y);
            PursuitBursts++;
        }

        void CrossBridgeIfNeeded(PoliceCarAI ai)
        {
            if (ai.HasDispatchRoute) return;
            float carX = ai.transform.position.x;
            bool toCity = session.Player.transform.position.x < 300f;
            if ((carX < 300f) == toCity) return;
            var route = new List<Vector3>();
            for (int step = 0; step < bridgeRoute.Length; step++)
            {
                int index = toCity ? step : bridgeRoute.Length - 1 - step;
                Vector3 point = bridgeRoute[index].position;
                if (toCity ? point.x >= carX - 4f : point.x <= carX + 4f) continue;
                if (point.x < 322f) point.z += toCity ? -2.5f : 2.5f;
                route.Add(point);
            }
            if (route.Count > 0) ai.SetDispatchRoute(route.ToArray());
        }

        void ParkAwayFromPlayer(Response response)
        {
            if (response.body.isKinematic || Vector3.Distance(session.Player.transform.position, response.ai.transform.position) < 70f) return;
            var camera = session.ViewCamera;
            Vector3 view = camera.WorldToViewportPoint(response.ai.transform.position);
            if (view.z > 0 && view.x > -.1f && view.x < 1.1f && view.y > -.1f && view.y < 1.1f) return;
            response.ai.enabled = false;
            response.body.linearVelocity = Vector3.zero;
            response.body.angularVelocity = Vector3.zero;
            response.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            response.body.isKinematic = true;
            response.body.position = response.home.position;
            response.body.rotation = response.home.rotation;
        }

        void Update()
        {
            if (session == null || fleet == null) return;
            // The helicopter has them in the light: nobody peels off to regroup while that lasts.
            bool spotted = PoliceChase.Current != null && PoliceChase.Current.Spotted;
            if (targetCar != null && !targetCar.gameObject.activeInHierarchy) StopWave();
            if (targetCar != null && session.Player.IsDriving && stationPatrol.policeCarAI.enabled)
                CrossBridgeIfNeeded(stationPatrol.policeCarAI);
            for (int i = 0; i < fleet.Length; i++)
            {
                var response = fleet[i];
                if (response.state == ResponseState.Parked) { ParkAwayFromPlayer(response); continue; }
                if (!session.Player.IsDriving)
                {
                    response.ai.PursuitEnabled = false;
                    response.nextChange += Time.deltaTime;
                    continue;
                }
                if (response.state == ResponseState.Queued)
                {
                    if (Time.time >= response.nextChange && ParkingAisleClear()) Launch(response, i);
                    continue;
                }
                response.ai.PursuitEnabled = response.state != ResponseState.Regrouping;
                if (response.state == ResponseState.Departing)
                {
                    if (!response.ai.HasDispatchRoute || response.ai.HasClearedParkingExit) BeginPursuit(response);
                    continue;
                }
                if (response.state == ResponseState.Pursuing)
                {
                    CrossBridgeIfNeeded(response.ai);
                    if (response.ai.HasDispatchRoute) { response.nextChange += Time.deltaTime; continue; }
                    if (!spotted && Time.time >= response.nextChange)
                    {
                        response.state = ResponseState.Regrouping;
                        response.ai.PursuitEnabled = false;
                        response.nextChange = Time.time + Random.Range(regroupDuration.x, regroupDuration.y);
                        RegroupCount++;
                    }
                }
                else if (spotted || Time.time >= response.nextChange) BeginPursuit(response);
            }
        }
        bool ParkingAisleClear()
        {
            // Opposing parking rows share an aisle. Let a car finish merging before releasing another.
            var lead = stationPatrol.policeCarAI;
            if (lead.enabled && lead.BlockingParkingAisle) return false;
            foreach (var car in fleet)
                if (car.ai.enabled && car.ai.BlockingParkingAisle) return false;
            return true;
        }
    }
}
