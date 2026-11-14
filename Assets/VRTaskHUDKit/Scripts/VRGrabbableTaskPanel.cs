using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(VRTaskHUD), typeof(BoxCollider), typeof(Rigidbody))]
public class VRGrabbableTaskPanel : MonoBehaviour
{
    [Tooltip("Открывать панель сразу при запуске")]
    public bool showAtStart;
    [Tooltip("Положение ВЕРХНЕГО ЛЕВОГО угла относительно головы, в метрах")]
    public Vector3 openOffset = new Vector3(-0.58f, 0.38f, 0.65f);
    [Min(0.01f)] public float grabThickness = 0.035f;
    VRTaskHUD hud;
    Canvas panelCanvas;
    BoxCollider grabCollider;
    XRGrabInteractable grab;
    Rigidbody body;
    InputAction toggleAction;
    bool visible, wasPressed;

    void Awake()
    {
        hud = GetComponent<VRTaskHUD>();
        panelCanvas = GetComponent<Canvas>();
        grabCollider = GetComponent<BoxCollider>();
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        hud.followCamera = false;
        body.useGravity = false;
        body.isKinematic = true;
        toggleAction = new InputAction("Toggle Task Panel", InputActionType.Button,
            "<XRController>{LeftHand}/secondaryButton");
    }
    void OnEnable() { toggleAction?.Enable(); wasPressed = false; }
    void Start() { SetVisible(showAtStart); }
    void OnDisable()
    {
        toggleAction?.Disable();
        if (panelCanvas) SetVisible(false);
    }
    void OnDestroy() { toggleAction?.Dispose(); }
    void Update()
    {
        bool pressed = toggleAction != null && toggleAction.IsPressed();
        if (pressed && !wasPressed) TogglePanel();
        wasPressed = pressed;
        UpdateBounds();
    }
    void UpdateBounds()
    {
        if (!hud.panel) return;
        Rect rect = hud.panel.rect;
        float scale = Mathf.Max(0.00001f, Mathf.Abs(transform.lossyScale.z));
        grabCollider.center = new Vector3(rect.center.x, rect.center.y, 0);
        grabCollider.size = new Vector3(rect.width, rect.height, grabThickness / scale);
    }
    public void TogglePanel() { SetVisible(!visible); }
    public void ShowPanel() { SetVisible(true); }
    public void HidePanel() { SetVisible(false); }
    void SetVisible(bool value)
    {
        // Keep this object active so it can still receive Y when the panel is hidden.
        if (grab) grab.enabled = false; // Cancels any current grab before moving/hiding.
        visible = value;
        if (value)
        {
            hud.Refresh();
            Camera camera = hud.headCamera ? hud.headCamera : Camera.main;
            if (camera)
            {
                Vector3 position = camera.transform.position + camera.transform.rotation *
                    new Vector3(openOffset.x, openOffset.y, Mathf.Max(0.35f, openOffset.z));
                transform.SetPositionAndRotation(position, camera.transform.rotation);
                body.position = position;
                body.rotation = camera.transform.rotation;
            }
            UpdateBounds();
        }
        if (panelCanvas) panelCanvas.enabled = value;
        grabCollider.enabled = value;
        if (grab) grab.enabled = value;
    }
}
