using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Standalone VR task list. No dependency on earlier VRTaskHUD scripts.
[DisallowMultipleComponent]
public class VRInspectionTaskHUD : MonoBehaviour
{
    [Serializable]
    public class TaskItem
    {
        [TextArea(1, 3)] public string text;
        public bool completed;
        public TaskItem(string value) { text = value; }
    }

    [Header("Camera of the active XR Origin")]
    public Camera headCamera;
    [Header("Room visibility")]
    public bool showOnlyInsideRoom = false;
    [Tooltip("Box covering the room, including the height of the player's head. Use Is Trigger.")]
    public BoxCollider roomArea;
    [Header("Room diagnostics (runtime)")]
    [SerializeField] bool playerInsideRoom;
    [Header("Tasks - event numbers start at 1")]
    public string title = "ЗАДАНИЯ";
    public List<TaskItem> tasks = new List<TaskItem>
    {
        new TaskItem("Проверить наличие кожуха"),
        new TaskItem("Включить дробитель")
    };
    [Header("Placement in metres relative to the head")]
    [Min(0.5f)] public float distance = 1.3f;
    public Vector2 offset = new Vector2(-0.42f, 0.18f);
    [Min(0.2f)] public float widthMetres = 0.52f;
    [Header("Appearance")]
    public Font font;
    [Range(20, 90)] public int fontSize = 42;
    public Color textColor = Color.white;
    public Color completedColor = new Color(0.2f, 1f, 0.4f);
    public Color backgroundColor = new Color(0.015f, 0.02f, 0.025f, 0.94f);
    public Color borderColor = new Color(0.15f, 0.8f, 0.35f);

    GameObject panel;
    RectTransform rect;
    Canvas canvas;
    Image border, background;
    Text heading, body;
    bool dirty = true;
    bool visible = true;
    const float PixelWidth = 900f;
    const float Padding = 30f;

    void Awake() { if (!headCamera) headCamera = Camera.main; }
    void OnEnable() { dirty = true; }
    void OnValidate() { dirty = true; }
    void LateUpdate()
    {
        if (!headCamera) headCamera = Camera.main;
        if (!headCamera)
        {
            playerInsideRoom = false;
            if (panel) panel.SetActive(false);
            return;
        }
        if (!panel) Build();
        if (dirty) RefreshHUD();
        UpdateVisibility();
        Transform head = headCamera.transform;
        rect.SetPositionAndRotation(head.TransformPoint(new Vector3(offset.x, offset.y,
            Mathf.Max(0.5f, distance))), head.rotation);
        rect.localScale = Vector3.one * (Mathf.Max(0.2f, widthMetres) / PixelWidth);
        canvas.worldCamera = headCamera;
    }

    public void CompleteTask(int taskNumber)
    {
        if (tasks == null || taskNumber < 1 || taskNumber > tasks.Count || tasks[taskNumber - 1] == null)
        {
            Debug.LogWarning("VRInspectionTaskHUD: task number must be between 1 and Tasks.Count.", this);
            return;
        }
        tasks[taskNumber - 1].completed = true;
        dirty = true;
    }
    public void ResetTasks()
    {
        if (tasks != null) foreach (TaskItem item in tasks) if (item != null) item.completed = false;
        dirty = true;
    }
    public void SetVisible(bool value) { visible = value; UpdateVisibility(); }
    public void ToggleVisible() => SetVisible(!visible);

    void UpdateVisibility()
    {
        playerInsideRoom = false;
        if (headCamera && roomArea && roomArea.enabled && roomArea.gameObject.activeInHierarchy)
        {
            Vector3 local = roomArea.transform.InverseTransformPoint(headCamera.transform.position) - roomArea.center;
            Vector3 half = roomArea.size * 0.5f;
            playerInsideRoom = Mathf.Abs(local.x) <= Mathf.Abs(half.x) &&
                Mathf.Abs(local.y) <= Mathf.Abs(half.y) && Mathf.Abs(local.z) <= Mathf.Abs(half.z);
        }
        if (panel) panel.SetActive(visible && isActiveAndEnabled && headCamera &&
            (!showOnlyInsideRoom || playerInsideRoom));
    }

    void Build()
    {
        panel = new GameObject("VR task panel (generated)", typeof(RectTransform), typeof(Canvas));
        panel.layer = headCamera.gameObject.layer;
        rect = panel.GetComponent<RectTransform>();
        canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        border = MakeImage("Border", 0f);
        background = MakeImage("Background", 4f);
        heading = MakeText("Heading");
        body = MakeText("Tasks");
        dirty = true;
    }

    [ContextMenu("Refresh HUD")]
    public void RefreshHUD()
    {
        dirty = true;
        if (!panel) return;
        Font chosen = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        heading.font = body.font = chosen;
        heading.fontSize = fontSize + 8;
        body.fontSize = fontSize;
        heading.text = title;
        heading.supportRichText = false;
        heading.color = body.color = textColor;
        var content = new StringBuilder();
        if (tasks != null)
            for (int i = 0; i < tasks.Count; i++)
            {
                TaskItem item = tasks[i];
                if (item == null) continue;
                if (content.Length > 0) content.Append("\n\n");
                string text = (item.text ?? "").Replace("<", "‹").Replace(">", "›");
                if (item.completed) content.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(completedColor)).Append('>');
                content.Append(i + 1).Append(". ").Append(text);
                if (item.completed) content.Append("\nВЫПОЛНЕНО</color>");
            }
        body.supportRichText = true;
        body.text = content.ToString();
        float y = Padding;
        PlaceText(heading, ref y);
        y += 20f;
        PlaceText(body, ref y);
        rect.sizeDelta = new Vector2(PixelWidth, y + Padding);
        border.color = borderColor;
        background.color = backgroundColor;
        dirty = false;
    }

    void PlaceText(Text text, ref float y)
    {
        RectTransform r = text.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(PixelWidth - Padding * 2f, 1f);
        float height = Mathf.Ceil(text.preferredHeight) + 5f;
        r.sizeDelta = new Vector2(PixelWidth - Padding * 2f, height);
        r.anchoredPosition = new Vector2(Padding, -y);
        y += height;
    }
    Image MakeImage(string objectName, float inset)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.layer = panel.layer;
        Image image = go.GetComponent<Image>();
        RectTransform r = image.rectTransform;
        r.SetParent(rect, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.one * inset;
        r.offsetMax = Vector2.one * -inset;
        image.raycastTarget = false;
        return image;
    }
    Text MakeText(string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.layer = panel.layer;
        Text text = go.GetComponent<Text>();
        text.rectTransform.SetParent(rect, false);
        text.alignment = TextAnchor.UpperLeft;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
    void OnDisable() { if (panel) panel.SetActive(false); }
    void OnDestroy() { if (panel) Destroy(panel); }
}
