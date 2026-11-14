using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public class VRTaskHUD : MonoBehaviour
{
    [Serializable]
    public class TaskItem
    {
        [TextArea(1, 4)] public string text;
        public bool completed;
        public TaskItem(string text) { this.text = text; }
    }

    [Header("Camera / Камера внутри XR Origin")]
    public Camera headCamera;
    public bool followCamera = true;
    [Header("Content / Текст")]
    public string tasksTitle = "ЗАДАЧА:";
    public List<TaskItem> tasks = new List<TaskItem>
    {
        new TaskItem("Осмотреть кожух"), new TaskItem("Включить дробитель")
    };
    public string safetyTitle = "! ТЕХНИКА БЕЗОПАСНОСТИ !";
    [TextArea(5, 15)] public string safetyText = "ВО ВРЕМЯ РАБОТЫ ДРОБИТЕЛЯ НИ В КОЕМ СЛУЧАЕ:\n\nНЕ СУЙТЕ РУКИ\nНЕ ПОДХОДИТЕ БЛИЗКО\nНЕ НАСТУПАЙТЕ";
    [Header("Position / Положение в метрах от камеры")]
    [Min(0.5f)] public float distance = 1.3f;
    public Vector2 topLeftOffset = new Vector2(-0.65f, 0.42f);
    [Min(0.0001f)] public float metresPerPixel = 0.001f;
    [Min(0.1f)] public float maximumHeight = 0.8f;
    [Header("Appearance / Оформление")]
    public Font font;
    [Min(250)] public float width = 590;
    [Range(14, 256)] public int titleFontSize = 28;
    [Range(14, 256)] public int taskFontSize = 28;
    [Range(14, 256)] public int safetyFontSize = 24;
    public FontStyle fontStyle = FontStyle.Normal;
    [Range(1, 2)] public float lineSpacing = 1;
    [Range(0, 60)] public float padding = 22;
    [Range(0, 60)] public float blockGap = 16;
    public Color textColor = Color.white;
    public Color safetyColor = new Color(1f, 0.85f, 0.3f);
    public Color backgroundColor = new Color(0.025f, 0.04f, 0.06f, 0.88f);
    [Header("Generated references / Заполняются генератором")]
    public RectTransform panel;
    public Image background;
    public Text taskHeading, taskBody, safetyHeading, safetyBody;
    bool dirty = true;

    void OnEnable() { dirty = true; }
    void OnValidate() { dirty = true; }
    void Update() { if (dirty) Refresh(); }
    void LateUpdate()
    {
        if (!followCamera || !headCamera || !panel) return;
        Transform head = headCamera.transform;
        transform.SetPositionAndRotation(head.TransformPoint(new Vector3(
            topLeftOffset.x, topLeftOffset.y, Mathf.Max(0.5f, distance))), head.rotation);
    }

    // Task numbers match the displayed numbers: 1, 2, 3...
    public void CompleteTask(int taskNumber)
    {
        if (tasks == null || taskNumber < 1 || taskNumber > tasks.Count) return;
        tasks[taskNumber - 1].completed = true;
        Refresh();
    }
    public void ResetTasks()
    {
        if (tasks != null) foreach (var task in tasks) if (task != null) task.completed = false;
        Refresh();
    }

    [ContextMenu("Refresh HUD")]
    public void Refresh()
    {
        dirty = false;
        if (!panel || !taskHeading || !taskBody || !safetyHeading || !safetyBody) return;
        var lines = new StringBuilder();
        if (tasks != null)
            for (int i = 0; i < tasks.Count; i++)
            {
                if (i > 0) lines.Append("\n");
                var item = tasks[i];
                lines.Append(i + 1).Append(") ").Append(item == null ? "" : item.text);
                if (item != null && item.completed) lines.Append(" [выполнено]");
            }
        float actualWidth = Mathf.Max(250, width);
        float inset = Mathf.Clamp(padding, 0, 60);
        float innerWidth = actualWidth - inset * 2;
        float y = inset;
        Place(taskHeading, tasksTitle, titleFontSize, textColor, innerWidth, inset, ref y);
        y += 8;
        Place(taskBody, lines.ToString(), taskFontSize, textColor, innerWidth, inset, ref y);
        y += Mathf.Max(0, blockGap);
        Place(safetyHeading, safetyTitle, titleFontSize, safetyColor, innerWidth, inset, ref y);
        y += 8;
        Place(safetyBody, safetyText, safetyFontSize, safetyColor, innerWidth, inset, ref y);
        float height = y + inset;
        panel.pivot = new Vector2(0, 1);
        panel.sizeDelta = new Vector2(actualWidth, height);
        float scale = Mathf.Min(Mathf.Max(0.0001f, metresPerPixel), Mathf.Max(0.1f, maximumHeight) / height);
        transform.localScale = Vector3.one * scale;
        if (background) background.color = backgroundColor;
        var canvas = GetComponent<Canvas>();
        if (canvas) canvas.worldCamera = headCamera;
    }

    void Place(Text label, string value, int size, Color color, float w, float x, ref float y)
    {
        if (font) label.font = font;
        label.text = value ?? "";
        label.fontSize = Mathf.Clamp(size, 14, 256);
        label.fontStyle = fontStyle;
        label.lineSpacing = lineSpacing;
        label.color = color;
        label.supportRichText = false;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.alignment = TextAnchor.UpperLeft;
        label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(w, 1);
        float h = Mathf.Ceil(label.preferredHeight) + 4;
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -y);
        y += h;
    }
}
