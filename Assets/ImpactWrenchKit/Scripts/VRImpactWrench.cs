using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class VRImpactWrench : MonoBehaviour
{
    public Transform socketTip;
    public Transform rotatingSocket;
    public Renderer directionIndicator;
    public int socketSizeMM = 24;
    [Min(0.01f)] public float turnsPerSecond = 2f;
    [Min(0.001f)] public float engagementDistance = 0.025f;
    [Range(0f, 90f)] public float alignmentAngle = 20f;
    public bool reverse;
    public UnityEvent<bool> onReverseChanged = new UnityEvent<bool>();
    [Header("Runtime diagnostics")]
    [SerializeField] bool triggerHeld;
    [SerializeField] VRThreadedNut engagedNut;
    XRGrabInteractable grab;
    IXRSelectInteractor operatingHand;
    bool previousPrimaryButton;
    bool primaryReady;
    MaterialPropertyBlock indicatorProperties;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        indicatorProperties = new MaterialPropertyBlock();
    }
    void OnEnable()
    {
        grab.activated.AddListener(Activate);
        grab.deactivated.AddListener(Deactivate);
        grab.selectExited.AddListener(Release);
        RefreshIndicator();
    }
    void OnDisable()
    {
        grab.activated.RemoveListener(Activate);
        grab.deactivated.RemoveListener(Deactivate);
        grab.selectExited.RemoveListener(Release);
        triggerHeld = false;
        engagedNut = null;
        operatingHand = null;
    }
    void Activate(ActivateEventArgs args) => triggerHeld = true;
    void Deactivate(DeactivateEventArgs args) => triggerHeld = false;
    void Release(SelectExitEventArgs args) { triggerHeld = false; engagedNut = null; }

    [ContextMenu("Toggle Direction")]
    public void ToggleDirection() => SetReverse(!reverse);
    public void SetReverse(bool value)
    {
        reverse = value;
        // Require a new trigger press after changing direction.
        triggerHeld = false;
        RefreshIndicator();
        onReverseChanged.Invoke(reverse);
    }

    void Update()
    {
        IXRSelectInteractor hand = null;
        foreach (var candidate in grab.interactorsSelecting)
            if (!(candidate is XRSocketInteractor)) { hand = candidate; break; }
        if (hand != operatingHand)
        {
            operatingHand = hand;
            primaryReady = false;
            previousPrimaryButton = false;
        }
        if (hand == null || !socketTip) { triggerHeld = false; engagedNut = null; return; }
        ReadDirectionButton(hand);
        RefreshIndicator();
        engagedNut = FindNut();
        if (!triggerHeld) return;
        float desired = turnsPerSecond * Time.deltaTime * (reverse ? 1f : -1f);
        float actual = engagedNut ? engagedNut.Drive(desired) : desired;
        // Tool +Z points into the panel, opposite the bolt's outward +Z.
        if (rotatingSocket) rotatingSocket.Rotate(Vector3.forward, -actual * 360f, Space.Self);
    }

    void ReadDirectionButton(IXRSelectInteractor hand)
    {
        if (hand.handedness == InteractorHandedness.None) return;
        XRNode node = hand.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed)) return;
        if (!primaryReady) { primaryReady = true; previousPrimaryButton = pressed; return; }
        if (pressed && !previousPrimaryButton) ToggleDirection();
        previousPrimaryButton = pressed;
    }

    VRThreadedNut FindNut()
    {
        VRThreadedNut nearest = null;
        float best = engagementDistance * engagementDistance;
        float minDot = Mathf.Cos(alignmentAngle * Mathf.Deg2Rad);
        foreach (var nut in VRThreadedNut.ActiveNuts)
        {
            if (!nut || !nut.isActiveAndEnabled || !nut.nutVisual || nut.socketSizeMM != socketSizeMM) continue;
            if (Vector3.Dot(socketTip.forward, -nut.OutwardAxis) < minDot) continue;
            Vector3 offset = socketTip.position - nut.ContactPosition;
            // Reject approaching the nut from behind the panel.
            if (Vector3.Dot(offset, nut.OutwardAxis) < -0.006f) continue;
            float distance = offset.sqrMagnitude;
            if (distance > best) continue;
            best = distance;
            nearest = nut;
        }
        return nearest;
    }

    void RefreshIndicator()
    {
        if (!directionIndicator || indicatorProperties == null) return;
        Color color = reverse ? new Color(1f, 0.15f, 0.025f) : new Color(0.05f, 0.8f, 0.15f);
        directionIndicator.GetPropertyBlock(indicatorProperties);
        indicatorProperties.SetColor("_BaseColor", color);
        indicatorProperties.SetColor("_Color", color);
        directionIndicator.SetPropertyBlock(indicatorProperties);
    }
}
