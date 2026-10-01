using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ValetGetaway.Editor
{
    public static class VehicleDamageValidation
    {
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        public static void Impact(VehicleInteraction car, float speed)
        {
            typeof(VehicleInteraction).GetMethod("ApplyImpact", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(car, new object[] { speed });
        }

        [MenuItem("Valet Getaway/Validate Vehicle Damage")]
        public static void Run()
        {
            var test = new GameObject("Vehicle damage validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var car = test.AddComponent<VehicleInteraction>();
                Check(car.Condition == 1f && car.SalePrice == 2500, "New car value");
                Impact(car, 3f);
                Impact(car, float.NaN);
                Check(car.Condition == 1f, "Gentle/invalid impact should not damage car");
                Impact(car, 10f);
                Check(Mathf.Abs(car.Condition - 0.685f) < 0.001f && car.SalePrice == 1870, "Crash value calculation");
                Impact(car, 10f);
                Check(car.SalePrice == 1870, "Contact burst cooldown");
                typeof(VehicleInteraction).GetField("nextDamageAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(car, 0f);
                Impact(car, 1000f);
                Check(car.Condition == 0f && car.SalePrice == 500, "Minimum salvage value");
                car.PrepareForGuest();
                Check(car.Condition == 1f && car.SalePrice == 2500, "Replacement car condition reset");
                Debug.Log("VALET DAMAGE VALIDATION PASSED: gentle impacts, invalid input, crash depreciation, cooldown, salvage floor, replacement reset.");
            }
            finally { UnityEngine.Object.DestroyImmediate(test); }
        }
    }
}
