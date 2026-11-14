using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class UpgradeVRTaskPanel
{
    [MenuItem("GameObject/VR Mechanics/Upgrade Selected HUD to Y Panel", false, 21)]
    public static void Upgrade()
    {
        var hud = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<VRTaskHUD>() : null;
        if (!hud || !hud.panel)
        {
            EditorUtility.DisplayDialog("Task Panel", "Выдели VR_TaskHUD в Hierarchy, затем повтори команду.", "OK");
            return;
        }
        if (Application.isPlaying) return;
        Undo.RecordObject(hud, "Upgrade HUD to grabbable panel");
        hud.followCamera = false;
        hud.width = 1520;
        hud.metresPerPixel = 0.0005f;
        hud.maximumHeight = 0.95f;
        hud.titleFontSize = 76;
        hud.taskFontSize = 80;
        hud.safetyFontSize = 72;
        hud.padding = 44;
        hud.blockGap = 32;
        hud.lineSpacing = 1.12f;
        hud.fontStyle = FontStyle.Bold;
        hud.backgroundColor = new Color(0.025f, 0.035f, 0.05f, 0.96f);
        foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true))
        {
            Undo.RecordObject(graphic, "Update panel material");
            graphic.material = null; // Normal depth: hands remain visible in front of the panel.
            graphic.raycastTarget = false;
        }
        var body = hud.GetComponent<Rigidbody>();
        if (!body) body = Undo.AddComponent<Rigidbody>(hud.gameObject);
        Undo.RecordObject(body, "Configure panel body");
        body.useGravity = false;
        body.isKinematic = true;
        var box = hud.GetComponent<BoxCollider>();
        if (!box) box = Undo.AddComponent<BoxCollider>(hud.gameObject);
        Undo.RecordObject(box, "Configure panel collider");
        box.isTrigger = true;
        var grab = hud.GetComponent<XRGrabInteractable>();
        if (!grab) grab = Undo.AddComponent<XRGrabInteractable>(hud.gameObject);
        Undo.RecordObject(grab, "Configure panel grab");
        grab.colliders.Clear();
        grab.colliders.Add(box);
        grab.interactionLayers = InteractionLayerMask.GetMask("Default");
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.snapToColliderVolume = false;
        var control = hud.GetComponent<VRGrabbableTaskPanel>();
        if (!control) control = Undo.AddComponent<VRGrabbableTaskPanel>(hud.gameObject);
        hud.Refresh();
        Rect rect = hud.panel.rect;
        box.center = new Vector3(rect.center.x, rect.center.y, 0);
        box.size = new Vector3(rect.width, rect.height, control.grabThickness / hud.transform.localScale.z);
        if (hud.headCamera)
            hud.transform.SetPositionAndRotation(hud.headCamera.transform.position +
                hud.headCamera.transform.rotation * control.openOffset, hud.headCamera.transform.rotation);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;
    }
}
