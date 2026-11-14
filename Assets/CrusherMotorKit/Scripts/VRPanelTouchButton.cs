using UnityEngine;
using UnityEngine.Events;

// Contact button for an existing imported mesh. No XR selection/Grip required.
// The box is checked geometrically, independent of physics trigger settings.
[RequireComponent(typeof(BoxCollider))]
public class VRPanelTouchButton : MonoBehaviour
{
    public BoxCollider pressArea;
    public Transform[] touchPoints = new Transform[2];
    [Min(0.001f)] public float touchRadius = 0.012f;
    [Min(0.001f)] public float releaseMargin = 0.008f;
    public UnityEvent onPressed = new UnityEvent();
    [Header("Button movement")]
    [Tooltip("Leave empty to move this button object.")]
    public Transform pressVisual;
    public Vector3 localPressDirection = Vector3.down;
    [Min(0f)] public float pressTravel = 0.004f;
    [Min(0.001f)] public float animationSpeed = 0.04f;
    [Header("Runtime diagnostics")]
    [SerializeField] bool isPressed;
    [SerializeField] int activeTouchPoints;
    [SerializeField] float nearestDistanceMetres = -1f;
    Vector3 restLocalPosition;
    float currentTravel;
    float testUntil;
    bool capturedRest;

    void Reset() => pressArea = GetComponent<BoxCollider>();
    void Awake()
    {
        if (!pressArea) pressArea = GetComponent<BoxCollider>();
        if (!pressVisual) pressVisual = transform;
        restLocalPosition = pressVisual.localPosition;
        capturedRest = true;
        isPressed = false;
    }
    void Update()
    {
        activeTouchPoints = 0;
        nearestDistanceMetres = -1f;
        if (!pressArea || !pressArea.enabled || !pressArea.gameObject.activeInHierarchy)
        {
            isPressed = false;
            return;
        }
        float threshold = touchRadius + (isPressed ? releaseMargin : 0f);
        bool touching = Time.time < testUntil;
        Matrix4x4 areaMatrix = GetRestAreaMatrix();
        Matrix4x4 areaInverse = areaMatrix.inverse;
        if (touchPoints != null) foreach (Transform point in touchPoints)
        {
            if (!point || !point.gameObject.activeInHierarchy) continue;
            activeTouchPoints++;
            // Compute oriented-box distance directly; no Physics.SyncTransforms needed.
            Vector3 local = areaInverse.MultiplyPoint3x4(point.position) - pressArea.center;
            Vector3 half = pressArea.size * 0.5f;
            Vector3 closest = new Vector3(Mathf.Clamp(local.x, -half.x, half.x),
                Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            Vector3 world = areaMatrix.MultiplyPoint3x4(closest + pressArea.center);
            float distance = Vector3.Distance(point.position, world);
            if (nearestDistanceMetres < 0f || distance < nearestDistanceMetres) nearestDistanceMetres = distance;
            if (distance <= threshold) touching = true;
        }
        bool wasPressed = isPressed;
        isPressed = touching;
        if (touching && !wasPressed) onPressed.Invoke();
    }
    // Keep the sensing region at the UNPRESSED position while the visible cap moves.
    // Otherwise a stationary hand repeatedly presses/releases a moving collider.
    Matrix4x4 GetRestAreaMatrix()
    {
        Matrix4x4 matrix = pressArea.transform.localToWorldMatrix;
        if (capturedRest && pressVisual &&
            (pressArea.transform == pressVisual || pressArea.transform.IsChildOf(pressVisual)))
        {
            Vector3 restWorld = pressVisual.parent
                ? pressVisual.parent.TransformPoint(restLocalPosition) : restLocalPosition;
            matrix = Matrix4x4.Translate(restWorld - pressVisual.position) * matrix;
        }
        return matrix;
    }

    void LateUpdate()
    {
        if (!capturedRest || !pressVisual) return;
        currentTravel = Mathf.MoveTowards(currentTravel, isPressed ? pressTravel : 0f, animationSpeed * Time.deltaTime);
        Vector3 axis = localPressDirection.sqrMagnitude > 0.000001f ? localPressDirection.normalized : Vector3.down;
        Vector3 worldOffset = pressVisual.TransformDirection(axis).normalized * currentTravel;
        Vector3 parentOffset = pressVisual.parent ? pressVisual.parent.InverseTransformVector(worldOffset) : worldOffset;
        pressVisual.localPosition = restLocalPosition + parentOffset;
    }

    void OnDisable()
    {
        isPressed = false;
        testUntil = 0f;
        currentTravel = 0f;
        if (capturedRest && pressVisual) pressVisual.localPosition = restLocalPosition;
    }

    [ContextMenu("Test Press (Play Mode Only)")]
    void TestPress()
    {
        if (!Application.isPlaying) { Debug.Log("Enter Play Mode first.", this); return; }
        isPressed = true;
        testUntil = Time.time + 0.3f;
        onPressed.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        var area = pressArea ? pressArea : GetComponent<BoxCollider>();
        Matrix4x4 previous = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        if (area)
        {
            Gizmos.matrix = Application.isPlaying && pressArea ? GetRestAreaMatrix() : area.transform.localToWorldMatrix;
            Gizmos.color = isPressed ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(area.center, area.size);
        }
        Gizmos.matrix = Matrix4x4.identity;
        if (touchPoints != null) foreach (var point in touchPoints)
        {
            if (!point) continue;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(point.position, touchRadius);
        }
        Gizmos.matrix = previous;
        Gizmos.color = previousColor;
    }
}
