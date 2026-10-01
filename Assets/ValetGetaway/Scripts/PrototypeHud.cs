using UnityEngine;

namespace ValetGetaway
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private static ArcadeVehicleController activeVehicle;
        private static VehicleInteraction interaction;
        private HotelGameSession session;

        private void Start() => session = Object.FindAnyObjectByType<HotelGameSession>();

        public static void SetVehicle(ArcadeVehicleController vehicle, VehicleInteraction vehicleInteraction)
        {
            activeVehicle = vehicle;
            interaction = vehicleInteraction;
        }

        private void OnGUI()
        {
            if (session != null) { DrawSession(); return; }
            GUIStyle panel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 16,
                padding = new RectOffset(14, 14, 10, 10)
            };

            string text;
            if (activeVehicle != null)
            {
                int speed = Mathf.RoundToInt(Mathf.Abs(activeVehicle.Speed) * 2.237f);
                text = $"{speed} MPH     E  Exit     Space  Brake";
                if (interaction != null && interaction.NearBuyer)
                {
                    text = interaction.CanDeliver ? "Parked     E  Exit and talk to buyer" : "Space  Brake     E  Exit";
                }
            }
            else if (interaction != null && interaction.CanTalkToBuyer)
            {
                text = interaction.CanDeliver ? "E  Talk to buyer and sell car" : "Buyer: Park the car nearby and stop it first.";
            }
            else if (interaction != null && interaction.CanEnter)
            {
                text = "E  Enter vehicle";
            }
            else
            {
                text = "WASD  Move     Shift  Sprint     Mouse  Look";
            }

            Vector2 size = panel.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(20f, Screen.height - 60f, Mathf.Max(330f, size.x), 40f), text, panel);
        }

        private static void Panel(Rect rect, string text, int fontSize, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.93f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize, fontStyle = FontStyle.Bold, wordWrap = true,
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 6, 6)
            };
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private void DrawSession()
        {
            float width = Screen.width - 24f;
            bool narrow = Screen.width < 740;
            float headerBottom = narrow ? 140f : 94f;
            if (narrow)
            {
                Panel(new Rect(12, 12, width, 50), $"MONEY  ${session.Money:N0}     |     SOLD  {session.Delivery.DeliveriesCompleted}", 23, new Color(0.45f, 1f, 0.7f));
                Panel(new Rect(12, 66, width, 70), session.Objective, 22, Color.white);
            }
            else
            {
                Panel(new Rect(12, 12, width - 264, 78), session.Objective, 25, Color.white);
                Panel(new Rect(Screen.width - 264, 12, 252, 78), $"MONEY  ${session.Money:N0}\nCARS SOLD  {session.Delivery.DeliveriesCompleted}", 24, new Color(0.45f, 1f, 0.7f));
            }
            Panel(new Rect(12, Screen.height - 82, width, 70), session.Prompt, 23, Color.white);
            if (!string.IsNullOrEmpty(session.Notification))
                Panel(new Rect(12, Screen.height - 154, width, 64), session.Notification, 21, new Color(1f, 0.83f, 0.3f));

            if (!string.IsNullOrEmpty(session.Notification)) return;
            Transform buyer = session.Delivery.VisibleBuyer;
            if (buyer != null) DrawDestination(buyer.position + Vector3.up * 2f, "BUYER", headerBottom, new Color(1f, 0.85f, 0.2f));
            else if (!session.Delivery.HasDelivery && session.ReservedCar == null && session.Money > 0)
            {
                HotelBusStop nearest = null;
                float distance = float.MaxValue;
                foreach (HotelBusStop stop in session.BusStops)
                {
                    float current = Vector3.Distance(stop.transform.position, session.Player.transform.position);
                    if (current < distance) { distance = current; nearest = stop; }
                }
                if (nearest != null) DrawDestination(nearest.transform.position + Vector3.up * 3f, "BUS", headerBottom, new Color(0.4f, 0.9f, 1f));
            }
        }

        private void DrawDestination(Vector3 destination, string name, float top, Color color)
        {
            Camera camera = session.ViewCamera;
            Vector3 point = camera.WorldToScreenPoint(destination);
            int metres = Mathf.RoundToInt(Vector3.Distance(destination, session.Player.transform.position));
            float width = Mathf.Min(270f, Screen.width - 24f);
            bool visible = point.z > 0 && point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height;
            float x = visible ? Mathf.Clamp(point.x - width / 2, 12, Screen.width - width - 12) : (Screen.width - width) / 2;
            float y = visible ? Mathf.Clamp(Screen.height - point.y, top + 6, Mathf.Max(top + 6, Screen.height - 218)) : top + 6;
            Vector3 local = camera.transform.InverseTransformPoint(destination);
            string direction = visible ? "" : local.z < 0 && Mathf.Abs(local.x) < Mathf.Abs(local.z) * 0.4f ? "  Behind" : local.x < 0 ? "  < Left" : "  Right >";
            Panel(new Rect(x, y, width, 52), $"{name}  {metres} m{direction}", 23, color);
        }
    }
}
