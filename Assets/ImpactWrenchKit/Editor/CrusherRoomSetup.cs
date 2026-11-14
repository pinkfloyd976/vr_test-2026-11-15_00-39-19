#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CrusherRoomSetup
{
    static readonly HashSet<string> SolidNames = new HashSet<string>
    {
        "Plane", "Handrails", "Columns_Galvanized", "Columns_Galvanized.001",
        "55 kW electric motor body", "Body_Lower", "Hood_Cover", "Bunker", "Bunker.001",
        "Feed_Chute_Box", "Feed_Gate_Chute", "Hopper", "RED COUPLING GUARD.001",
        "Service_Door", "Inspection_Hatch", "Panel_Cabinet", "Panel_Mini", "Panel_S1",
        "Panel_S2", "Panel_S3", "Junction_Boxes"
    };

    [MenuItem("GameObject/VR Mechanics/Setup Selected Room Collisions", false, 30)]
    static void SetupRoom()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject root = Selection.activeGameObject;
        if (!root || EditorUtility.IsPersistent(root))
        {
            EditorUtility.DisplayDialog("Select room", "Select the room root in Hierarchy first.", "OK");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure room collisions");
        var added = new List<string>();
        var skipped = new List<string>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            string name = filter.gameObject.name;
            bool known = SolidNames.Contains(name) || name.StartsWith("Wall_Brick") || name.StartsWith("Wall_Panels");
            if (!known || !filter.sharedMesh) continue;
            // Only stationary geometry; don't give a moving rigidbody a concave collider.
            if (filter.GetComponentInParent<Rigidbody>() || filter.GetComponentInParent<XRBaseInteractable>())
            {
                skipped.Add(name + " (moving/interactable object)");
                continue;
            }
            bool hasSolid = false;
            foreach (var existing in filter.GetComponents<Collider>())
                if (existing.enabled && !existing.isTrigger) { hasSolid = true; break; }
            if (hasSolid) continue;
            MeshCollider collider = null;
            foreach (var existing in filter.GetComponents<MeshCollider>())
                if (!existing.enabled && !existing.isTrigger) { collider = existing; break; }
            if (!collider) collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
            Undo.RecordObject(collider, "Configure static collider");
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            collider.enabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            added.Add(name);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Room collision setup. Added/enabled: " + added.Count + "\n" + string.Join("\n", added)
            + "\nSkipped: " + string.Join(", ", skipped), root);
        EditorUtility.DisplayDialog("Room collisions", "Added/enabled colliders: " + added.Count
            + ". Details are in Console. Save the scene. Existing solid colliders were preserved.", "OK");
    }

    [MenuItem("GameObject/VR Mechanics/Setup Selected Wrench Gravity", false, 31)]
    static void SetupWrench()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var selected = Selection.activeGameObject;
        var grab = selected ? selected.GetComponent<XRGrabInteractable>() : null;
        var body = selected ? selected.GetComponent<Rigidbody>() : null;
        if (!grab || !body)
        {
            EditorUtility.DisplayDialog("Select wrench", "Select ImpactWrench root with XR Grab Interactable and Rigidbody.", "OK");
            return;
        }
        if (typeof(VRWrenchDrop).Assembly.GetName().Name.Contains("Editor"))
        {
            EditorUtility.DisplayDialog("Script location", "VRWrenchDrop.cs must be outside every Editor folder.", "OK");
            return;
        }
        Undo.RecordObjects(new Object[] { grab, body }, "Enable wrench gravity");
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = 1.5f;
        body.linearDamping = 0.5f;
        body.angularDamping = 2f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = false;
        if (!selected.GetComponent<VRWrenchDrop>()) Undo.AddComponent<VRWrenchDrop>(selected);
        PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        PrefabUtility.RecordPrefabInstancePropertyModifications(grab);
        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log("Wrench gravity enabled. Throw On Detach is off. Place the wrench above a surface with a solid collider, then save the scene.", selected);
    }
}
#endif
