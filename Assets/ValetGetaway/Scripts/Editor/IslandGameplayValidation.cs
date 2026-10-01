using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class IslandGameplayValidation
    {
        const string Key="Valet.Island.Playtest";
        static HotelGameSession session;
        static HotelGuestCar guest;
        static VehicleInteraction car;
        static Rigidbody body;
        static Keyboard keyboard;
        static InputSettings.BackgroundBehavior originalInputBackground;
        static InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        static bool originalBackground;
        static int phase,payout;
        static double started;
        static float phaseTime;
        static Vector3 buyerStart;
        static bool buyerMoved;
        static Vector3 P(float x,float y,float h=.4f)=>new Vector3(x-500,h,240-y);
        static IslandGameplayValidation() { EditorApplication.playModeStateChanged+=OnPlay; }
        [MenuItem("Valet Getaway/Validate Island Gameplay")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(IslandCityBuilder.ScenePath);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        public static void RunBatch() { SessionState.SetBool(Key+".batch",true); Run(); }
        static void OnPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            phase=0; started=EditorApplication.timeSinceStartup; buyerMoved=false;
            originalBackground=Application.runInBackground; Application.runInBackground=true;
            originalInputBackground=InputSystem.settings.backgroundBehavior;
            originalEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();
            EditorApplication.update+=Tick;
        }
        static void Assert(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        static void Drive(bool down) => InputSystem.QueueStateEvent(keyboard,down ? new KeyboardState(UnityEngine.InputSystem.Key.W) : new KeyboardState());
        static void PositionCar(Vector3 position,float heading)
        {
            body.linearVelocity=Vector3.zero; body.angularVelocity=Vector3.zero; body.position=position; body.rotation=Quaternion.Euler(0,heading,0);
            car.transform.SetPositionAndRotation(position,body.rotation); Physics.SyncTransforms();
        }
        static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>210) throw new TimeoutException("phase="+phase+" guest="+guest?.State+" position="+guest?.transform.position);
                if(phase==0)
                {
                    session=Object.FindAnyObjectByType<HotelGameSession>(); if(session==null || Time.time<1) return;
                    guest=session.Guests[0]; car=guest.GetComponent<VehicleInteraction>(); body=car.GetComponent<Rigidbody>();
                    Assert(session.Guests.Length==3 && session.BusStops.Length==5,"Incomplete island gameplay");
                    foreach(var buyer in session.Delivery.Buyers) Assert(buyer.GetComponent<NavMeshAgent>().isOnNavMesh,"Buyer not on navigation mesh");
                    buyerStart=session.Delivery.Buyers[0].position; phase=1;
                }
                if(phase==1)
                {
                    buyerMoved|=Vector3.Distance(buyerStart,session.Delivery.Buyers[0].position)>.5f;
                    if(guest.State!=HotelGuestCar.ArrivalState.Waiting) return;
                    Assert(buyerMoved,"Buyers did not wander");
                    Assert(!car.CanEnter,"Unclaimed car enterable");
                    session.Player.TeleportTo(guest.Guest.position+new Vector3(-.8f,-1,-.5f),Quaternion.identity);
                    Assert(guest.HandOverKeys(session),"Island guest handover failed");
                    Assert(!guest.HandOverKeys(session),"Duplicate keys accepted");
                    session.Player.TeleportTo(car.transform.position+Vector3.left*1.8f,Quaternion.identity); car.EnterVehicle();
                    Assert(session.Player.IsDriving && session.Delivery.HasDelivery && !session.Delivery.BuyerDiscovered,"Entry or hidden buyer assignment failed");
                    Assert(!session.TakeBus(),"Bus accepted driver");
                    PositionCar(P(790,247),270); Drive(true); phaseTime=Time.time; phase=2;
                }
                if(phase==2)
                {
                    Assert(body.position.y>-.5f,"Car fell through bridge");
                    if(body.position.x>140) { var controller=car.GetComponent<ArcadeVehicleController>(); Assert(Time.time-phaseTime<18,"Bridge crossing stalled at "+body.position+" velocity="+body.linearVelocity+" driver="+controller.HasDriver+" W="+Keyboard.current?.wKey.isPressed+" throttle="+controller.LastThrottle+" grounded="+controller.Grounded+" enabled="+controller.enabled+" kinematic="+body.isKinematic); return; }
                    Drive(false); Debug.Log("ISLAND CHECK: physical bridge drive passed");
                    PositionCar(P(565,302),90); Drive(true); phaseTime=Time.time; phase=3;
                }
                if(phase==3)
                {
                    if(body.position.x<135) { Assert(Time.time-phaseTime<20,"Jump ramp drive stalled at "+body.position); return; }
                    Assert(body.position.y>6,"Ramp did not launch the car");
                    Drive(false); Debug.Log("ISLAND CHECK: jump ramp launch passed at "+body.position);
                    PositionCar(P(700,302,-4),90); phaseTime=Time.time; phase=4; return;
                }
                if(phase==4)
                {
                    if(Time.time-phaseTime<.3f) return;
                    Assert(body.position.y>0,"Water recovery failed");
                    Assert(session.Player.IsDriving && session.Delivery.HasDelivery,"Recovery lost the active job");
                    var delivery=session.Delivery;
                    foreach(var buyer in delivery.Buyers) { buyer.GetComponent<BuyerWander>().enabled=false; buyer.GetComponent<NavMeshAgent>().isStopped=true; }
                    int index=(int)typeof(BuyerDeliveryManager).GetField("selectedBuyer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(delivery);
                    var target=delivery.Buyers[index]; target.GetComponent<NavMeshAgent>().Warp(P(530,247,1.22f));
                    PositionCar(P(530,244),180);
                    session.ViewCamera.transform.position=P(530,235,4); session.ViewCamera.transform.LookAt(target.position);
                    Physics.SyncTransforms();
                    typeof(BuyerDeliveryManager).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(delivery,null);
                    Assert(delivery.BuyerDiscovered,"Visible city buyer not discovered");
                    Assert(!delivery.TrySell(car),"Sale accepted inside car");
                    car.ExitVehicle(); session.Player.TeleportTo(target.position+new Vector3(1.6f,-1,0),Quaternion.identity); Physics.SyncTransforms();
                    body.linearVelocity=Vector3.forward*4; Assert(!delivery.TrySell(car),"Moving sale accepted"); body.linearVelocity=Vector3.zero;
                    PositionCar(P(530,237),180); Assert(!delivery.TrySell(car),"Distant sale accepted"); PositionCar(P(530,244),180);
                    VehicleDamageValidation.Impact(car,10); payout=car.SalePrice; Assert(payout<guest.SalePrice,"Damage did not affect value");
                    Assert(delivery.TrySell(car),"Stopped nearby on-foot sale failed");
                    Assert(session.Money==payout && !delivery.TrySell(car),"Incorrect or duplicate payout");
                    session.Player.TeleportTo(session.BusStops[2].transform.position+Vector3.back*2+Vector3.up*.3f,Quaternion.identity);
                    Assert(session.TakeBus() && !session.TakeBus(),"Bus return/cooldown failed");
                    phase=5;
                }
                if(phase==5 && guest.State==HotelGuestCar.ArrivalState.Waiting)
                {
                    session.Player.TeleportTo(guest.Guest.position+new Vector3(-.8f,-1,-.5f),Quaternion.identity);
                    Assert(guest.HandOverKeys(session),"Replacement guest route or handover failed");
                    Assert(car.Condition==1 && session.Money==payout,"Replacement condition/wallet wrong");
                    Finish(true,"Arrival, keys, walking buyers, real bridge driving, ramp launch, water recovery, discovery, sale gates, damage value, bus return and replacement arrival passed.");
                }
            }
            catch(Exception e) { Finish(false,e.ToString()); }
        }
        static void Finish(bool passed,string detail)
        {
            EditorApplication.update-=Tick; SessionState.SetBool(Key,false);
            string result="VALET ISLAND GAMEPLAY VALIDATION "+(passed ? "PASSED: " : "FAILED: ")+detail;
            File.WriteAllText("Logs/IslandGameplayValidation.txt",result); if(passed) Debug.Log(result); else Debug.LogError(result);
            if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=originalInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=originalEditorInput;
            Application.runInBackground=originalBackground;
            EditorApplication.isPlaying=false;
            if(SessionState.GetBool(Key+".batch",false)) { SessionState.SetBool(Key+".batch",false); EditorApplication.Exit(passed?0:1); }
        }
    }
}
