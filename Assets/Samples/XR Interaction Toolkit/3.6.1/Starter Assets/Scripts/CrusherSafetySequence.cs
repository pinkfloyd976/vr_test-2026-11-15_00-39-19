using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Connect guard completion to RegisterGuardInspection, motor start to RegisterMotorStart.
// This records order and displays a warning; it does not stop the motor.
[DisallowMultipleComponent]
public class CrusherSafetySequence : MonoBehaviour
{
    public Camera headCamera;
    [Tooltip("Assign the motor's AudioSource to detect playback starting automatically. Then do not also call RegisterMotorStart from a button.")]
    public AudioSource motorAudioSource;
    public Font font;
    [Min(1f)] public float displaySeconds = 7f;
    [Min(0.5f)] public float panelDistance = 1f;
    [Min(0.3f)] public float panelWidth = 0.85f;
    [Min(0.2f)] public float panelHeight = 0.4f;
    [Range(20, 70)] public int fontSize = 38;
    [TextArea(3, 6)] public string violationReason =
        "Вы включили двигатель дробителя до проверки наличия защитного кожуха.\n\nСначала необходимо осмотреть кожух, затем включать двигатель.";
    [Header("Events")]
    public UnityEvent onSafeMotorStart = new UnityEvent();
    public UnityEvent onViolation = new UnityEvent();
    [Header("Runtime diagnostics")]
    [SerializeField] bool guardInspected;
    [SerializeField] bool violationOccurred;
    [SerializeField] int violationCount;
    public bool GuardInspected => guardInspected;
    public bool ViolationOccurred => violationOccurred;
    GameObject panel;
    RectTransform panelRect;
    float hideAt;
    bool wasMotorPlaying;

    void Awake()
    {
        guardInspected = false;
        violationOccurred = false;
        violationCount = 0;
        wasMotorPlaying = false;
        if (!headCamera) headCamera = Camera.main;
    }

    public void RegisterGuardInspection()
    {
        if (!Application.isPlaying) return;
        guardInspected = true;
        // A later inspection never erases an already recorded violation.
    }

    public void RegisterMotorStart()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (guardInspected)
        {
            onSafeMotorStart.Invoke();
            return;
        }
        violationOccurred = true;
        violationCount++;
        ShowViolation();
        onViolation.Invoke();
    }

    [ContextMenu("TEST - Show violation (Play Mode)")]
    public void ShowViolation()
    {
        if (!Application.isPlaying) return;
        if (!headCamera) headCamera = Camera.main;
        if (!headCamera) { Debug.LogWarning("CrusherSafetySequence: assign Head Camera.", this); return; }
        if (panel) Destroy(panel);
        panel = new GameObject("Crusher safety violation", typeof(RectTransform), typeof(Canvas));
        panel.layer = headCamera.gameObject.layer;
        panelRect = panel.GetComponent<RectTransform>();
        float width = Mathf.Max(0.3f, panelWidth);
        panelRect.sizeDelta = new Vector2(1000f, Mathf.Max(0.2f, panelHeight) / width * 1000f);
        panelRect.localScale = Vector3.one * (width / 1000f);
        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = headCamera;
        canvas.sortingOrder = 200;
        AddImage("Red border", new Color(1f, 0.12f, 0.08f), 0f);
        AddImage("Black background", Color.black, 6f);
        GameObject labelObject = new GameObject("Reason", typeof(RectTransform), typeof(Text));
        labelObject.layer = panel.layer;
        Text label = labelObject.GetComponent<Text>();
        label.rectTransform.SetParent(panelRect, false);
        Stretch(label.rectTransform, 30f);
        label.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.supportRichText = true;
        label.text = "<color=#FF5544>НАРУШЕНИЕ ТЕХНИКИ БЕЗОПАСНОСТИ</color>\n\n" +
            (violationReason ?? "").Replace("<", "‹").Replace(">", "›");
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        hideAt = Time.unscaledTime + Mathf.Max(1f, displaySeconds);
        PlacePanel();
    }

    void LateUpdate()
    {
        bool motorPlaying = motorAudioSource && motorAudioSource.isPlaying;
        if (motorPlaying && !wasMotorPlaying) RegisterMotorStart();
        wasMotorPlaying = motorPlaying;
        if (!panel || !panel.activeSelf) return;
        if (!headCamera || Time.unscaledTime >= hideAt) panel.SetActive(false);
        else PlacePanel();
    }
    void PlacePanel()
    {
        Transform head = headCamera.transform;
        panelRect.SetPositionAndRotation(head.TransformPoint(new Vector3(0f, -0.05f,
            Mathf.Max(0.5f, panelDistance))), head.rotation);
    }
    void AddImage(string objectName, Color color, float inset)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.layer = panel.layer;
        Image image = go.GetComponent<Image>();
        image.rectTransform.SetParent(panelRect, false);
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
    void OnDisable() { if (panel) panel.SetActive(false); }
    void OnDestroy() { if (panel) Destroy(panel); }
}
