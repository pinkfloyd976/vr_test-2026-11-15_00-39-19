using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class VRTool : MonoBehaviour
{
    public string toolType = "Screwdriver";
    public Transform tip;
    public float radius = 0.025f;
    public LayerMask targetLayers;
    XRGrabInteractable grab;
    bool activated;
    void Awake() => grab = GetComponent<XRGrabInteractable>();
    void OnEnable()
    {
        grab.activated.AddListener(Activate);
        grab.deactivated.AddListener(Deactivate);
        grab.selectExited.AddListener(Release);
    }
    void OnDisable()
    {
        grab.activated.RemoveListener(Activate);
        grab.deactivated.RemoveListener(Deactivate);
        grab.selectExited.RemoveListener(Release);
        activated = false;
    }
    void Activate(ActivateEventArgs args) => activated = true;
    void Deactivate(DeactivateEventArgs args) => activated = false;
    void Release(SelectExitEventArgs args) => activated = false;
    void Update()
    {
        if (!activated || !tip || !grab.isSelected) return;
        bool heldByHand = false;
        foreach (var interactor in grab.interactorsSelecting)
            if (!(interactor is XRSocketInteractor)) heldByHand = true;
        if (!heldByHand) return;
        // One target per frame, even if the target has multiple colliders.
        foreach (Collider hit in Physics.OverlapSphere(tip.position, radius, targetLayers, QueryTriggerInteraction.Collide))
        {
            ToolTarget target = hit.GetComponentInParent<ToolTarget>();
            if (target && target.Accepts(toolType))
            {
                target.Apply(Time.deltaTime);
                break;
            }
        }
    }
}
