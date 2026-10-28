// Turns the decorative helicopter on the precinct helipad into the chase's air unit, and checks the rest of the
// chase wiring is in place.
//
//   Valet Getaway > Police Station > Set Up Helicopter Chase
//
// The model, the helipad and every other piece of the police set are left exactly where they are: this only takes
// the colliders and static flags off the helicopter so it can fly, hangs a searchlight under it, and adds
// PoliceHelicopter with its rotors wired up.

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class PoliceChaseSetup
    {
        const string PoliceRoot = "07 Police station - precinct";

        [MenuItem("Valet Getaway/Police Station/Set Up Helicopter Chase", priority = 218)]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[PoliceChase] Edit mode only."); return; }
            var scene = SceneManager.GetActiveScene();

            var root = GameObject.Find(PoliceRoot);
            if (root == null) { Debug.LogError($"[PoliceChase] No '{PoliceRoot}'. Run Build Site first."); return; }

            var helicopter = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.IndexOf("Helicopter", StringComparison.OrdinalIgnoreCase) >= 0);
            if (helicopter == null) { Debug.LogError("[PoliceChase] No PD_PoliceHelicopter under the precinct."); return; }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up helicopter chase");

            // It has to move, so it cannot be a static, solid part of the scenery any more.
            foreach (var t in helicopter.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                foreach (var collider in t.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
            }

            var air = helicopter.GetComponent<PoliceHelicopter>();
            if (air == null) air = Undo.AddComponent<PoliceHelicopter>(helicopter.gameObject);

            var main = Find(helicopter, "MainRotor");
            var tail = Find(helicopter, "TailRotor");
            var light = BuildSearchLight(helicopter);

            var serialized = new SerializedObject(air);
            serialized.FindProperty("mainRotor").objectReferenceValue = main;
            serialized.FindProperty("tailRotor").objectReferenceValue = tail;
            serialized.FindProperty("searchLight").objectReferenceValue = light;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The chase state itself rides on the session, and is created on demand, but wire it now so it shows up
            // in the inspector where it can be tuned.
            var session = Object.FindAnyObjectByType<HotelGameSession>();
            if (session != null && session.GetComponent<PoliceChase>() == null)
                Undo.AddComponent<PoliceChase>(session.gameObject);

            int rams = 0;
            foreach (var ai in Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include))
                if (ai.GetComponent<PoliceRam>() == null) { Undo.AddComponent<PoliceRam>(ai.gameObject); rams++; }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[PoliceChase] Air unit ready on '{helicopter.name}': main rotor {(main != null ? "found" : "MISSING")}, " +
                      $"tail rotor {(tail != null ? "found" : "MISSING")}, searchlight attached, " +
                      $"{rams} cruiser(s) given a PIT behaviour. " +
                      (session != null ? "Chase state on " + session.name + "." : "No HotelGameSession in the scene."));
        }

        static Transform Find(Transform root, string key) => root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>A downward spotlight under the nose, in police-searchlight white.</summary>
        static Light BuildSearchLight(Transform helicopter)
        {
            var existing = helicopter.Find("Searchlight");
            if (existing == null)
            {
                var go = new GameObject("Searchlight");
                Undo.RegisterCreatedObjectUndo(go, "Searchlight");
                go.transform.SetParent(helicopter, false);
                go.transform.localPosition = new Vector3(0f, -.6f, 1.4f);
                existing = go.transform;
            }
            existing.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = existing.GetComponent<Light>();
            if (light == null) light = Undo.AddComponent<Light>(existing.gameObject);
            light.type = LightType.Spot;
            light.spotAngle = 34f;
            light.innerSpotAngle = 14f;
            light.range = 140f;
            light.intensity = 420f;
            light.color = new Color(.96f, .98f, 1f);
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.enabled = false;
            return light;
        }
    }
}
