using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class VRValve : MonoBehaviour, IXRSelectFilter
{
    public Vector3 localAxis = Vector3.forward;
    public float minAngle = 0f;
    public float maxAngle = 720f;
    public float angle;
    [Header("Rim grab area (world metres)")]
    [Min(0.02f)] public float rimRadius = 0.15f;
    [Min(0.001f)] public float rimTolerance = 0.045f;
    [Min(0.001f)] public float maxGrabDepth = 0.08f;
    [Min(0f)] public float holdSlack = 0.03f;
    public UnityEvent<float> onValueChanged = new UnityEvent<float>();

    XRSimpleInteractable interactable;
    Quaternion initialRotation;
    Transform hand;
    Vector3 previousDirection;
    bool hasPreviousDirection;
    public float Value => Mathf.InverseLerp(minAngle, maxAngle, angle);
    public bool canProcess => isActiveAndEnabled;
    Vector3 Axis => localAxis.sqrMagnitude > 0.000001f ? localAxis.normalized : Vector3.forward;
    float InnerRadius => Mathf.Max(0.01f, rimRadius - rimTolerance);
    Quaternion FrameRotation => transform.parent
        ? transform.parent.rotation * initialRotation : initialRotation;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        initialRotation = transform.localRotation;
        angle = Mathf.Clamp(angle, minAngle, maxAngle);
        Apply();
    }

    void OnEnable()
    {
        interactable.selectFilters.Add(this);
        interactable.selectEntered.AddListener(BeginGrab);
        interactable.selectExited.AddListener(EndGrab);
    }

    void OnDisable()
    {
        interactable.selectFilters.Remove(this);
        interactable.selectEntered.RemoveListener(BeginGrab);
        interactable.selectExited.RemoveListener(EndGrab);
        hand = null;
        hasPreviousDirection = false;
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
    {
        // Preserve selection while held. Rotation pauses outside the permitted area.
        if (interactable.interactorsSelecting.Contains(interactor)) return true;
        Transform point = interactor.GetAttachTransform(target);
        return point && TryDirection(point.position, false, out _);
    }

    void BeginGrab(SelectEnterEventArgs args)
    {
        hand = args.interactorObject.GetAttachTransform(interactable);
        hasPreviousDirection = hand && TryDirection(hand.position, false, out previousDirection);
    }

    void EndGrab(SelectExitEventArgs args)
    {
        hand = null;
        hasPreviousDirection = false;
    }

    bool TryDirection(Vector3 position, bool holding, out Vector3 direction)
    {
        // The reference frame follows the base, but does not rotate with the wheel.
        Vector3 offset = Quaternion.Inverse(FrameRotation) * (position - transform.position);
        float depth = Vector3.Dot(offset, Axis);
        direction = offset - Axis * depth;
        float radius = direction.magnitude;
        float slack = holding ? holdSlack : 0f;
        // Never relax the inner exclusion, even during an existing grab.
        if (radius < InnerRadius || radius > rimRadius + rimTolerance + slack
            || Mathf.Abs(depth) > maxGrabDepth + slack) return false;
        direction /= radius;
        return true;
    }

    void Update()
    {
        if (!hand || !interactable.isSelected) { hasPreviousDirection = false; return; }
        if (!TryDirection(hand.position, true, out Vector3 current))
        {
            hasPreviousDirection = false;
            return;
        }
        if (!hasPreviousDirection)
        {
            previousDirection = current;
            hasPreviousDirection = true;
            return;
        }
        float delta = Vector3.SignedAngle(previousDirection, current, Axis);
        previousDirection = current;
        float next = Mathf.Clamp(angle + delta, minAngle, maxAngle);
        if (Mathf.Approximately(next, angle)) return;
        angle = next;
        Apply();
        onValueChanged.Invoke(Value);
    }

    void Apply() => transform.localRotation = initialRotation * Quaternion.AngleAxis(angle, Axis);

    void OnValidate()
    {
        rimRadius = Mathf.Max(0.02f, rimRadius);
        rimTolerance = Mathf.Clamp(rimTolerance, 0.001f, rimRadius - 0.01f);
        maxGrabDepth = Mathf.Max(0.001f, maxGrabDepth);
        holdSlack = Mathf.Max(0f, holdSlack);
        maxAngle = Mathf.Max(minAngle, maxAngle);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 normal = transform.TransformDirection(Axis).normalized;
        Vector3 u = Vector3.Cross(normal, Vector3.up);
        if (u.sqrMagnitude < 0.01f) u = Vector3.Cross(normal, Vector3.right);
        u.Normalize();
        Vector3 v = Vector3.Cross(normal, u);
        DrawCircle(InnerRadius, Color.red, u, v);
        DrawCircle(rimRadius, Color.yellow, u, v);
        DrawCircle(rimRadius + rimTolerance, Color.green, u, v);
    }

    void DrawCircle(float radius, Color color, Vector3 u, Vector3 v)
    {
        Gizmos.color = color;
        Vector3 previous = transform.position + u * radius;
        for (int i = 1; i <= 64; i++)
        {
            float radians = i * Mathf.PI * 2f / 64f;
            Vector3 next = transform.position + radius * (u * Mathf.Cos(radians) + v * Mathf.Sin(radians));
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
