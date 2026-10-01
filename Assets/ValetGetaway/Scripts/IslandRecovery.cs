using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    public sealed class IslandRecovery:MonoBehaviour
    {
        [SerializeField] HotelGameSession session;
        [SerializeField] Transform hotelReturn;
        readonly Dictionary<VehicleInteraction,Pose> safePlaces=new Dictionary<VehicleInteraction,Pose>();
        public void Configure(HotelGameSession value,Transform returnPoint) { session=value; hotelReturn=returnPoint; }
        void Update()
        {
            foreach(var guest in session.Guests)
            {
                if(!guest.KeysReceived || !guest.gameObject.activeInHierarchy) continue;
                var car=guest.GetComponent<VehicleInteraction>();
                if(!safePlaces.ContainsKey(car)) safePlaces[car]=new Pose(guest.QueuePoint,Quaternion.identity);
                var body=guest.GetComponent<Rigidbody>();
                if(Physics.Raycast(body.position+Vector3.up*.2f,Vector3.down,out var hit,1.5f) && hit.normal.y>.98f && Mathf.Abs(body.linearVelocity.y)<1)
                    safePlaces[car]=new Pose(body.position,Quaternion.Euler(0,body.rotation.eulerAngles.y,0));
                if(body.position.y< -3 || session.ReservedCar==car && Keyboard.current!=null && Keyboard.current.rKey.wasPressedThisFrame) Recover(car);
            }
            if(!session.Player.IsDriving && session.Player.transform.position.y< -3)
            {
                session.Player.TeleportTo(hotelReturn.position,hotelReturn.rotation);
                session.Notify("Back at the hotel. Your current job is still active.");
            }
        }
        public void Recover(VehicleInteraction car)
        {
            if(car==null || !safePlaces.TryGetValue(car,out var pose)) return;
            var body=car.GetComponent<Rigidbody>(); body.linearVelocity=Vector3.zero; body.angularVelocity=Vector3.zero;
            body.position=pose.position+Vector3.up*.5f; body.rotation=pose.rotation;
            car.transform.SetPositionAndRotation(body.position,body.rotation);
            session.Notify("Car recovered. R recovers a stuck car.");
        }
    }
}
