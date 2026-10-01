using UnityEngine;

namespace ValetGetaway
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private static ArcadeVehicleController activeVehicle;
        private static VehicleInteraction interaction;
        private HotelGameSession session;
        private void Start() => session = Object.FindAnyObjectByType<HotelGameSession>();
        public static void SetVehicle(ArcadeVehicleController vehicle, VehicleInteraction value) { activeVehicle=vehicle; interaction=value; }
        private void OnGUI() { if (session != null) DrawSession(); }

        private static void Text(Rect rect,string value,int size,Color color,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            var style=new GUIStyle(GUI.skin.label) { fontSize=size,fontStyle=FontStyle.Bold,wordWrap=true,alignment=anchor };
            style.normal.textColor=new Color(0,0,0,.75f); GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),value,style);
            style.normal.textColor=color; GUI.Label(rect,value,style);
        }
        private void DrawSession()
        {
            float width=Screen.width-32f; bool narrow=Screen.width<740; float headerBottom=narrow?94f:70f;
            if(narrow)
            {
                Text(new Rect(16,10,width,28),$"${session.Money:N0}   •   {session.Delivery.DeliveriesCompleted} sold",18,new Color(.55f,1f,.75f));
                Text(new Rect(16,39,width,48),session.Objective,18,Color.white);
            }
            else
            {
                Text(new Rect(16,10,width-300,40),session.Objective,20,Color.white);
                Text(new Rect(Screen.width-284,10,268,40),$"${session.Money:N0}   •   {session.Delivery.DeliveriesCompleted} sold",18,new Color(.55f,1f,.75f),TextAnchor.MiddleRight);
            }
            VehicleInteraction car=session.ReservedCar;
            if(car!=null)
            {
                Color conditionColor=Color.Lerp(new Color(1,.35f,.25f),new Color(.55f,1f,.75f),car.Condition);
                string speed=activeVehicle!=null?$"{Mathf.RoundToInt(Mathf.Abs(activeVehicle.Speed)*2.237f)} MPH   •   ":"";
                Text(new Rect(16,headerBottom,width,30),$"{speed}condition {car.Condition:P0}   •   sale ${car.SalePrice:N0}",17,conditionColor); headerBottom+=34;
            }
            if(session.SpeechTarget!=null && !string.IsNullOrEmpty(session.SpeechText)) DrawSpeechBubble(session.SpeechTarget,session.SpeechText);
            Transform buyer=session.Delivery.VisibleBuyer;
            if(buyer!=null) DrawDestination(buyer.position+Vector3.up*2f,"BUYER",headerBottom,new Color(1,.85f,.2f));
            else if(!session.Delivery.HasDelivery && session.ReservedCar==null && session.Money>0)
            {
                HotelBusStop nearest=null; float distance=float.MaxValue;
                foreach(HotelBusStop stop in session.BusStops){float current=Vector3.Distance(stop.transform.position,session.Player.transform.position);if(current<distance){distance=current;nearest=stop;}}
                if(nearest!=null) DrawDestination(nearest.transform.position+Vector3.up*3f,"BUS",headerBottom,new Color(.4f,.9f,1));
            }
        }
        private void DrawDestination(Vector3 destination,string name,float top,Color color)
        {
            Camera camera=session.ViewCamera; Vector3 point=camera.WorldToScreenPoint(destination); int metres=Mathf.RoundToInt(Vector3.Distance(destination,session.Player.transform.position));
            float width=Mathf.Min(250,Screen.width-32); bool visible=point.z>0&&point.x>0&&point.x<Screen.width&&point.y>0&&point.y<Screen.height;
            float x=visible?Mathf.Clamp(point.x-width/2,16,Screen.width-width-16):(Screen.width-width)/2; float y=visible?Mathf.Clamp(Screen.height-point.y,top+4,Screen.height-130):top+4;
            Vector3 local=camera.transform.InverseTransformPoint(destination); string direction=visible?"":local.z<0&&Mathf.Abs(local.x)<Mathf.Abs(local.z)*.4f?"  Behind":local.x<0?"  < Left":"  Right >";
            Text(new Rect(x,y,width,32),$"{name}  {metres} m{direction}",17,color,TextAnchor.MiddleCenter);
        }
        private void DrawSpeechBubble(Transform speaker,string message)
        {
            Vector3 point=session.ViewCamera.WorldToScreenPoint(speaker.position+Vector3.up*3.1f); if(point.z<=0) return;
            float width=Mathf.Min(360,Screen.width-32),height=54; float x=Mathf.Clamp(point.x-width/2,16,Screen.width-width-16); float y=Mathf.Clamp(Screen.height-point.y-height,100,Screen.height-height-100);
            Rect bubble=new Rect(x,y,width,height);
            Color previous=GUI.color;
            GUI.color=new Color(.96f,.98f,1f,.92f);
            GUI.DrawTexture(bubble,Texture2D.whiteTexture);
            GUI.color=previous;
            Text(bubble,message,18,new Color(.06f,.1f,.14f,1f),TextAnchor.MiddleCenter);
        }
    }
}
