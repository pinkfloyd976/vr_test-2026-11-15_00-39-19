using UnityEngine;
using UnityEngine.XR;

// A visual inspection pointer only. Does not teleport, grab or activate objects.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(LineRenderer))]
[DisallowMultipleComponent]
public class VRStraightPointer : MonoBehaviour
{
    [Header("Hold the RIGHT index trigger to show the pointer")]
    public bool requireRightTrigger = true;
    [Range(0.05f, 1f)] public float triggerThreshold = 0.5f;
    [Tooltip("Optional. If empty, this object's position and local +Z are used.")]
    public Transform rayOrigin;
    [Min(0.1f)] public float maxDistance = 6f;
    [Min(0.0005f)] public float width = 0.004f;
    public LayerMask hitLayers = ~0;
    public Material lineMaterial;
    public Color color = Color.red;
    [Header("Runtime diagnostics")]
    [SerializeField] bool rightControllerAvailable;
    [SerializeField] bool triggerHeld;
    [SerializeField] float triggerValue;
    [SerializeField] Collider hitCollider;
    [SerializeField] float hitDistance;
    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.enabled = false;
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (!lineMaterial && !line.sharedMaterial)
            Debug.LogWarning("VRStraightPointer: assign an Unlit material to Line Material.", this);
    }

    void LateUpdate()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        rightControllerAvailable = device.isValid;
        triggerValue = 0f;
        triggerHeld = false;
        if (rightControllerAvailable)
        {
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float value))
            {
                triggerValue = value;
                triggerHeld = value >= triggerThreshold;
            }
            else if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
            {
                triggerHeld = pressed;
                triggerValue = pressed ? 1f : 0f;
            }
        }
        if (requireRightTrigger && !triggerHeld)
        {
            line.enabled = false;
            hitCollider = null;
            hitDistance = 0f;
            return;
        }
        Transform origin = rayOrigin ? rayOrigin : transform;
        Vector3 start = origin.position;
        Vector3 direction = origin.forward;
        float distance = Mathf.Max(0.1f, maxDistance);
        hitCollider = null;
        if (Physics.Raycast(start, direction, out RaycastHit hit, distance,
            hitLayers, QueryTriggerInteraction.Ignore))
        {
            distance = hit.distance;
            hitCollider = hit.collider;
        }
        hitDistance = distance;
        if (lineMaterial) line.sharedMaterial = lineMaterial;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = Mathf.Max(0.0005f, width);
        line.SetPosition(0, start);
        line.SetPosition(1, start + direction * distance);
        line.enabled = true;
    }

    void OnDisable()
    {
        if (line) line.enabled = false;
        hitCollider = null;
        hitDistance = 0f;
        triggerHeld = false;
        triggerValue = 0f;
        rightControllerAvailable = false;
    }
    void OnDrawGizmosSelected()
    {
        Transform origin = rayOrigin ? rayOrigin : transform;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin.position, origin.forward * 0.25f);
    }
}
