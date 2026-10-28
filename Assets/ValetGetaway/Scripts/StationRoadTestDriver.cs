#if UNITY_EDITOR
using UnityEngine;

namespace ValetGetaway.Editor
{
    // Play-mode-only probe for the isolated road tests. Never saved to a scene or included in a build.
    public sealed class StationRoadTestDriver : MonoBehaviour
    {
        VehicleGroundMotor motor;
        public string LastObstacle { get; private set; }
        void Awake() { motor=gameObject.AddComponent<VehicleGroundMotor>(); }
        void OnCollisionEnter(Collision collision)
        {
            foreach(var contact in collision.contacts)
                if(Mathf.Abs(contact.normal.y)<.7f) LastObstacle=collision.collider.name;
        }
        void FixedUpdate()
        {
            motor.Drive(1,0,false,new VehicleGroundMotor.Tuning {acceleration=20,reverseAcceleration=14,steering=100,maxSpeed=12,grip=6,braking=8,alignment=8});
        }
    }
}
#endif
