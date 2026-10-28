using UnityEngine;

namespace ValetGetaway
{
    /// <summary>
    /// The state of one police chase, shared by the cars, the helicopter and the HUD: when the wave started, when a
    /// cruiser last made contact, whether the player is currently spotted from the air, and how badly the stolen car
    /// has been beaten up.
    ///
    /// Added automatically to the scene the first time a car is taken, so nothing has to be wired by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceChase : MonoBehaviour
    {
        [Tooltip("Seconds of a chase with no cruiser making contact before the helicopter is scrambled.")]
        [SerializeField, Min(5f)] float airSupportAfter = 30f;
        [Tooltip("Condition at or below which the car is a write-off and the run is lost.")]
        [SerializeField, Range(0f, .5f)] float wreckedAt = .08f;
        [Tooltip("Seconds pinned at a standstill with police alongside before the player is busted.")]
        [SerializeField, Min(1f)] float bustAfter = 3.5f;

        HotelGameSession session;
        float waveStarted, lastContact, pinned;
        bool announcedAir;

        public static PoliceChase Current { get; private set; }

        /// <summary>The car the player stole, while a chase is running.</summary>
        public VehicleInteraction PlayerCar { get; private set; }
        public ArcadeVehicleController PlayerVehicle { get; private set; }
        public Rigidbody PlayerBody { get; private set; }
        public bool Active => PlayerCar != null && session != null && session.Player.IsDriving;

        /// <summary>The helicopter is up: 30 s of chase went by without a cruiser laying a hand on the player.</summary>
        public bool AirSupportCalled => Active && Time.time - lastContact >= airSupportAfter && Time.time - waveStarted >= airSupportAfter;

        /// <summary>The helicopter currently has the player in its light, so the cars know exactly where to go.</summary>
        public bool Spotted { get; set; }

        /// <summary>Where the air unit last called the player in.</summary>
        public Vector3 SpottedPosition { get; set; }

        /// <summary>How beaten up the stolen car is, 1 fresh to 0 scrap.</summary>
        public float Condition => PlayerCar != null ? PlayerCar.Condition : 1f;

        /// <summary>The car is damaged enough to be limping: down on power and vague in the steering.</summary>
        public bool Limping => Active && Condition < .45f;

        public static PoliceChase Ensure(HotelGameSession session)
        {
            if (Current == null)
            {
                Current = session.gameObject.GetComponent<PoliceChase>();
                if (Current == null) Current = session.gameObject.AddComponent<PoliceChase>();
            }
            Current.session = session;
            return Current;
        }

        void Awake()
        {
            Current = this;
            if (session == null) session = GetComponent<HotelGameSession>();
            if (session == null) session = Object.FindAnyObjectByType<HotelGameSession>();
        }

        void OnEnable() { if (session != null) session.CarTaken += Begin; }
        void OnDisable() { if (session != null) session.CarTaken -= Begin; }

        void Begin(VehicleInteraction car)
        {
            PlayerCar = car;
            PlayerVehicle = car != null ? car.GetComponent<ArcadeVehicleController>() : null;
            PlayerBody = car != null ? car.GetComponent<Rigidbody>() : null;
            waveStarted = lastContact = Time.time;
            pinned = 0f;
            announcedAir = false;
            Spotted = false;
        }

        /// <summary>A cruiser touched the player: the clock on air support restarts.</summary>
        public void ReportContact()
        {
            lastContact = Time.time;
            Spotted = false;
        }

        public void End()
        {
            PlayerCar = null; PlayerVehicle = null; PlayerBody = null;
            Spotted = false; pinned = 0f;
        }

        void Update()
        {
            if (session == null) return;
            if (PlayerCar != null && !session.Player.IsDriving) { End(); return; }
            if (!Active) return;

            if (AirSupportCalled && !announcedAir)
            {
                announcedAir = true;
                session.Notify("Air support: a police helicopter is up and looking for you.");
            }

            // Limp mode: a battered car is down on power and wanders under braking.
            if (PlayerVehicle != null)
            {
                float condition = Condition;
                PlayerVehicle.PowerScale = Mathf.Lerp(.42f, 1f, Mathf.Clamp01(condition));
                PlayerVehicle.SteeringScale = Mathf.Lerp(.68f, 1f, Mathf.Clamp01(condition));
            }

            if (Condition <= wreckedAt) { Bust("Your car is wrecked. The police have you."); return; }

            // Pinned in: stopped, with cruisers right alongside.
            bool stopped = PlayerBody != null && PlayerBody.linearVelocity.magnitude < 2f;
            pinned = stopped && PoliceAlongside() >= 2 ? pinned + Time.deltaTime : 0f;
            if (pinned >= bustAfter) Bust("Busted. The police boxed you in.");
        }

        int PoliceAlongside()
        {
            int count = 0;
            if (PlayerCar == null) return 0;
            foreach (var ram in PoliceRam.Active)
            {
                if (ram == null || !ram.isActiveAndEnabled) continue;
                if ((ram.transform.position - PlayerCar.transform.position).sqrMagnitude < 64f) count++;
            }
            return count;
        }

        void Bust(string message)
        {
            var car = PlayerCar;
            End();
            if (session != null)
            {
                session.Notify(message);
                session.Busted(car);
            }
        }
    }
}
