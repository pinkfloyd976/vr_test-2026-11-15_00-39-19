using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Uses the visible controller LineRenderer, including curved multi-segment rays.
// Attach to a separate, always-active manager object, not to the removable guard.
[DisallowMultipleComponent]
public class VRGuardInspection : MonoBehaviour
{
    [Header("Guard")]
    public Transform guard;
    [Tooltip("Optional stationary marker at the correct guard pivot position and rotation.")]
    public Transform requiredPose;
    [Min(0f)] public float positionTolerance = 0.05f;
    [Range(0f, 180f)] public float angleTolerance = 15f;

    [Header("Existing lasers - assign their LineRenderer components")]
    public LineRenderer leftLaser;
    public LineRenderer rightLaser;
    [Tooltip("Include the guard and objects which must block inspection, e.g. walls.")]
    public LayerMask raycastMask = ~0;
    [Min(0.1f)] public float maxInspectionDistance = 4f;
    [Min(0.05f)] public float requiredAimSeconds = 1.2f;

    [Header("VR notification")]
    public Camera headCamera;
    public Font font;
    [TextArea(2, 5)] public string message = "КОЖУХ НА МЕСТЕ\nПроверка выполнена";
    [Min(0.5f)] public float notificationSeconds = 4f;
    [Min(0.5f)] public float panelDistance = 1.2f;
    public Vector2 panelOffset = new Vector2(0f, -0.12f);
    [Min(0.2f)] public float panelWidthMetres = 0.75f;
    [Min(0.1f)] public float panelHeightMetres = 0.24f;
    [Range(20, 80)] public int fontSize = 44;
    [Range(2, 20)] public int borderPixels = 5;
    public Color borderColor = new Color(0.1f, 1f, 0.3f, 1f);

    [Header("Task completed - connect your HUD here")]
    public UnityEvent onCompleted = new UnityEvent();
    [Header("Runtime diagnostics")]
    [SerializeField] bool completed;
    [SerializeField] bool guardInPlace;
    [SerializeField] bool aimingAtGuard;
    [SerializeField] float aimSeconds;
    [SerializeField, TextArea(2, 4)] string leftRayStatus;
    [SerializeField, TextArea(2, 4)] string rightRayStatus;
    [SerializeField] Collider leftHitCollider;
    [SerializeField] Collider rightHitCollider;
    public bool Completed => completed;

    GameObject popup;
    RectTransform popupRect;
    float hideAt;
    const float CanvasWidth = 1000f;

    void Awake()
    {
        completed = false;
        if (!headCamera) headCamera = Camera.main;
    }

    void LateUpdate()
    {
        // XRI and line visuals update during the frame; inspect the drawn points late.
        guardInPlace = IsGuardInPlace();
        bool leftHit = HitsGuard(leftLaser, out leftRayStatus, out leftHitCollider);
        bool rightHit = HitsGuard(rightLaser, out rightRayStatus, out rightHitCollider);
        aimingAtGuard = !completed && guardInPlace && (leftHit || rightHit);
        if (!completed)
        {
            aimSeconds = aimingAtGuard ? aimSeconds + Time.deltaTime : 0f;
            if (aimSeconds >= Mathf.Max(0.05f, requiredAimSeconds)) CompleteInspection();
        }
        if (!popup || !popup.activeSelf) return;
        if (Time.unscaledTime >= hideAt || !headCamera)
        {
            popup.SetActive(false);
            return;
        }
        Transform head = headCamera.transform;
        popupRect.SetPositionAndRotation(head.TransformPoint(new Vector3(panelOffset.x,
            panelOffset.y, Mathf.Max(0.5f, panelDistance))), head.rotation);
    }

    bool IsGuardInPlace()
    {
        if (!guard || !guard.gameObject.activeInHierarchy) return false;
        if (!requiredPose) return true;
        return Vector3.Distance(guard.position, requiredPose.position) <= positionTolerance &&
               Quaternion.Angle(guard.rotation, requiredPose.rotation) <= angleTolerance;
    }

    bool HitsGuard(LineRenderer line, out string status, out Collider firstCollider)
    {
        firstCollider = null;
        if (!line) { status = "Линия не назначена"; return false; }
        if (!line.gameObject.activeInHierarchy) { status = "Объект линии выключен: " + line.name; return false; }
        if (!line.enabled) { status = "Line Renderer выключен: " + line.name; return false; }
        if (line.positionCount < 2) { status = "В линии меньше двух точек"; return false; }
        float remaining = Mathf.Max(0.1f, maxInspectionDistance);
        float checkedLength = 0f;
        Vector3 start = WorldPoint(line, 0);
        for (int i = 1; i < line.positionCount && remaining > 0f; i++)
        {
            Vector3 end = WorldPoint(line, i);
            Vector3 segment = end - start;
            float length = segment.magnitude;
            if (length > 0.00001f)
            {
                float distance = Mathf.Min(length, remaining);
                // Small endpoint tolerance because XR lines can end exactly on the collider surface.
                float castDistance = Mathf.Min(distance + 0.003f, remaining);
                if (Physics.Raycast(start, segment / length, out RaycastHit hit, castDistance,
                    raycastMask, QueryTriggerInteraction.Ignore))
                {
                    Transform hitObject = hit.collider.transform;
                    firstCollider = hit.collider;
                    // First solid hit blocks inspection. No rays through walls.
                    bool matches = guard && (hitObject == guard || hitObject.IsChildOf(guard));
                    status = (matches ? "ПОПАДАНИЕ В КОЖУХ: " : "ПЕРВЫМ ПОПАЛ В ДРУГОЙ ОБЪЕКТ: ") +
                        ObjectPath(hitObject) + "\nРасстояние по лучу: " + (checkedLength + hit.distance).ToString("F2") + " м";
                    return matches;
                }
                remaining -= distance;
                checkedLength += distance;
            }
            start = end;
        }
        status = "НЕТ ПОПАДАНИЯ. Проверено линии: " + checkedLength.ToString("F2") +
            " м. Проверь коллайдер кожуха, Is Trigger, слои и длину указки.";
        return false;
    }

    static string ObjectPath(Transform target)
    {
        string path = target.name;
        while (target.parent) { target = target.parent; path = target.name + "/" + path; }
        return path;
    }

    static Vector3 WorldPoint(LineRenderer line, int index)
    {
        Vector3 point = line.GetPosition(index);
        return line.useWorldSpace ? point : line.transform.TransformPoint(point);
    }

    void CompleteInspection()
    {
        completed = true;
        aimingAtGuard = false;
        ShowNotification();
        onCompleted.Invoke();
    }

    public void ResetInspection()
    {
        completed = false;
        aimingAtGuard = false;
        aimSeconds = 0f;
        if (popup) popup.SetActive(false);
    }

    [ContextMenu("TEST - Show notification (Play Mode)")]
    public void ShowNotification()
    {
        if (!Application.isPlaying) return;
        if (!headCamera) headCamera = Camera.main;
        if (!headCamera)
        {
            Debug.LogWarning("VRGuardInspection: assign the XR Origin Main Camera.", this);
            return;
        }
        if (popup) Destroy(popup);
        Font chosenFont = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        popup = new GameObject("Guard inspection - notification", typeof(RectTransform), typeof(Canvas));
        popup.layer = headCamera.gameObject.layer;
        popupRect = popup.GetComponent<RectTransform>();
        float width = Mathf.Max(0.2f, panelWidthMetres);
        float height = Mathf.Max(0.1f, panelHeightMetres) / width * CanvasWidth;
        popupRect.sizeDelta = new Vector2(CanvasWidth, height);
        popupRect.localScale = Vector3.one * (width / CanvasWidth);
        var canvas = popup.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = headCamera;
        canvas.sortingOrder = 100;
        AddImage("Green border", popupRect, borderColor, 0);
        AddImage("Black background", popupRect, Color.black, borderPixels);
        var textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
        textObject.layer = popup.layer;
        var label = textObject.GetComponent<Text>();
        label.rectTransform.SetParent(popupRect, false);
        Stretch(label.rectTransform, borderPixels + 20);
        label.font = chosenFont;
        label.text = message;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.supportRichText = false;
        label.raycastTarget = false;
        hideAt = Time.unscaledTime + Mathf.Max(0.5f, notificationSeconds);
        Transform head = headCamera.transform;
        popupRect.SetPositionAndRotation(head.TransformPoint(new Vector3(panelOffset.x,
            panelOffset.y, Mathf.Max(0.5f, panelDistance))), head.rotation);
    }

    void AddImage(string objectName, RectTransform parent, Color color, float inset)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.layer = popup.layer;
        var image = go.GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        Stretch(image.rectTransform, inset);
        image.color = color;
        image.raycastTarget = false;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    void OnDisable()
    {
        aimSeconds = 0f;
        aimingAtGuard = false;
        if (popup) popup.SetActive(false);
    }
    void OnDestroy() { if (popup) Destroy(popup); }
}
