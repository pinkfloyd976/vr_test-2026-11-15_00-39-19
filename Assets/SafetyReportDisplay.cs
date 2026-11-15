using UnityEngine;
using UnityEngine.UI;

// Creates a world-space VR report automatically. No prebuilt TMP panel is required.
public class SafetyReportDisplay : MonoBehaviour
{
    public Camera headCamera;
    public SafetyViolationManager manager;
    public Font font;
    [Min(1f)] public float displaySeconds = 12f;
    [Min(0.5f)] public float distance = 1.2f;
    [Min(0.3f)] public float width = 0.9f;
    [Min(0.2f)] public float height = 0.55f;
    [Range(20, 80)] public int fontSize = 38;
    GameObject panel;
    RectTransform rect;
    float hideAt;

    public void ShowReport()
    {
        if (!Application.isPlaying) return;
        var target = manager ? manager : SafetyViolationManager.Instance;
        if (target) ShowReportText(target.BuildReport(), target.TotalCount == 0);
    }
    public void ShowReportText(string report, bool success)
    {
        if (!Application.isPlaying) return;
        if (!headCamera) headCamera = Camera.main;
        if (!headCamera) { Debug.LogWarning("Assign report Head Camera.", this); return; }
        if (panel) Destroy(panel);
        panel = new GameObject("Room safety report", typeof(RectTransform), typeof(Canvas));
        panel.layer = headCamera.gameObject.layer;
        rect = panel.GetComponent<RectTransform>();
        float actualWidth = Mathf.Max(0.3f, width);
        rect.sizeDelta = new Vector2(1000f, Mathf.Max(0.2f, height) / actualWidth * 1000f);
        rect.localScale = Vector3.one * actualWidth / 1000f;
        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = headCamera;
        canvas.sortingOrder = 300;
        AddImage("Border", success ? new Color(0.1f, 1f, 0.3f) : new Color(1f, 0.3f, 0.1f), 0f);
        AddImage("Background", Color.black, 6f);
        var go = new GameObject("Report text", typeof(RectTransform), typeof(Text));
        go.layer = panel.layer;
        Text label = go.GetComponent<Text>();
        label.rectTransform.SetParent(rect, false);
        Stretch(label.rectTransform, 30f);
        label.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = report;
        label.supportRichText = false;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        hideAt = Time.unscaledTime + Mathf.Max(1f, displaySeconds);
        Place();
    }
    void LateUpdate()
    {
        if (!panel || !panel.activeSelf) return;
        if (!headCamera || Time.unscaledTime >= hideAt) HideReport();
        else Place();
    }
    void Place()
    {
        Transform head = headCamera.transform;
        rect.SetPositionAndRotation(head.TransformPoint(new Vector3(0f, -0.05f, Mathf.Max(0.5f, distance))), head.rotation);
    }
    void AddImage(string name, Color color, float inset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = panel.layer;
        Image image = go.GetComponent<Image>();
        image.rectTransform.SetParent(rect, false);
        Stretch(image.rectTransform, inset);
        image.color = color; image.raycastTarget = false;
    }
    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.one * inset; r.offsetMax = Vector2.one * -inset;
    }
    public void HideReport() { if (panel) panel.SetActive(false); }
    public void StartOver()
    {
        var target = manager ? manager : SafetyViolationManager.Instance;
        if (target) target.StartNewSession();
    }
    void OnDisable() { HideReport(); }
    void OnDestroy() { if (panel) Destroy(panel); }
}
