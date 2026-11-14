using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Remove the hand's residual velocity once on release, then let gravity act normally.
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class VRWrenchDrop : MonoBehaviour
{
    Rigidbody body;
    XRGrabInteractable grab;
    bool pendingDrop;
    void Awake() { body = GetComponent<Rigidbody>(); grab = GetComponent<XRGrabInteractable>(); }
    void OnEnable() => grab.selectExited.AddListener(Released);
    void OnDisable() { grab.selectExited.RemoveListener(Released); pendingDrop = false; }
    void Released(SelectExitEventArgs args) => pendingDrop = true;
    void FixedUpdate()
    {
        if (!pendingDrop) return;
        pendingDrop = false;
        if (grab.isSelected || body.isKinematic) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = true;
    }
}
