using System.Collections.Generic;
using UnityEngine;

namespace ValetGetaway
{
    /// <summary>
    /// Visual weight transfer. The chassis itself is kept upright by the motor's frozen X/Z rotation, which is what
    /// keeps the physics stable, so the bodywork is leaned instead: it rolls out of a corner and pitches under power
    /// and braking. Purely cosmetic - no collider, seat or AI transform is touched.
    ///
    /// Added automatically by <see cref="VehicleGroundMotor"/>, so every car and police cruiser gets it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleBodyLean : MonoBehaviour
    {
        [Tooltip("Degrees of body roll at one g of cornering.")]
        [SerializeField, Range(0f, 12f)] float rollPerG = 4.5f;
        [Tooltip("Degrees of pitch (squat and dive) at one g.")]
        [SerializeField, Range(0f, 12f)] float pitchPerG = 3.2f;
        [Tooltip("How quickly the body settles. Lower is softer suspension.")]
        [SerializeField, Min(1f)] float settle = 7f;

        static readonly string[] Keep = { "DriverSeat", "ExitPoint", "VehicleAccessPoint", "Seat", "Exit" };

        VehicleGroundMotor motor;
        Transform shell;
        float roll, pitch;

        void Awake()
        {
            motor = GetComponent<VehicleGroundMotor>();
            shell = BuildShell();
        }

        /// <summary>Gather the bodywork under one child so it can be leaned as a unit.</summary>
        Transform BuildShell()
        {
            var existing = transform.Find("Bodywork");
            if (existing != null) return existing;

            var movable = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.GetComponentInChildren<Renderer>(true) == null) continue;
                bool keep = false;
                foreach (var name in Keep)
                    if (child.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) { keep = true; break; }
                if (!keep) movable.Add(child);
            }
            if (movable.Count == 0) return null;

            var body = new GameObject("Bodywork").transform;
            body.SetParent(transform, false);
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one;
            foreach (var child in movable) child.SetParent(body, true);
            return body;
        }

        void LateUpdate()
        {
            if (shell == null || motor == null) return;
            float k = Mathf.Clamp01(settle * Time.deltaTime);
            roll = Mathf.Lerp(roll, Mathf.Clamp(motor.LateralG, -1.5f, 1.5f) * rollPerG, k);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(-motor.LongitudinalG, -1.5f, 1.5f) * pitchPerG, k);
            shell.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
