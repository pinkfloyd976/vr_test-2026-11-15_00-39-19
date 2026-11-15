using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreateCrusherSafetySign
{
    const string Folder = "Assets/CrusherSafetySignKit";
    [MenuItem("GameObject/VR Mechanics/Crusher Safety Sign", false, 35)]
    public static void Create()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Safety Sign", "Сначала останови Play.", "OK");
            return;
        }
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("ТЕХНИКА БЕЗОПАСНОСТИ — ДРОБИТЕЛЬ");
        Undo.RegisterCreatedObjectUndo(root, "Create crusher safety sign");
        var canvasObject = new GameObject("Табличка", typeof(RectTransform), typeof(Canvas), typeof(Image));
        canvasObject.transform.SetParent(root.transform, false);
        canvasObject.transform.localScale = Vector3.one * 0.00075f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1600, 1120);
        var border = canvasObject.GetComponent<Image>();
        border.color = new Color(0.035f, 0.04f, 0.045f, 1);
        border.raycastTarget = false;
        Color white = new Color(0.98f, 0.98f, 0.96f, 1);
        Color yellow = new Color(1f, 0.79f, 0.04f, 1);
        var header = Panel(rect, "Заголовок", 20, 20, 1560, 220, yellow);
        Label(header, "ТЕХНИКА БЕЗОПАСНОСТИ", font, 83, 30, 20);
        var row1 = Panel(rect, "Правило 1", 20, 256, 1560, 334, white);
        Label(row1, "ЗАПРЕЩЕНО ТРОГАТЬ ДРОБИТЕЛЬ\nВО ВРЕМЯ ЕГО РАБОТЫ", font, 74, 42, 26);
        var row2 = Panel(rect, "Правило 2", 20, 606, 1560, 236, white);
        Label(row2, "НЕ ПОДХОДИТЕ БЛИЗКО\nК ДРОБИТЕЛЮ", font, 74, 42, 18);
        var row3 = Panel(rect, "Правило 3", 20, 858, 1560, 242, white);
        Label(row3, "НЕ НАСТУПАЙТЕ\nНА ДРОБИТЕЛЬ", font, 74, 42, 18);

        if (Camera.main)
        {
            var camera = Camera.main.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            root.transform.SetPositionAndRotation(camera.position + forward * 1.5f, Quaternion.LookRotation(forward, Vector3.up));
        }
        else if (SceneView.lastActiveSceneView)
        {
            root.transform.position = SceneView.lastActiveSceneView.pivot;
        }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "CrusherSafetySignKit");
        if (!AssetDatabase.IsValidFolder(Folder + "/Prefabs")) AssetDatabase.CreateFolder(Folder, "Prefabs");
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Prefabs/CrusherSafetySign.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        ViewSelected();
        Debug.Log("Safety sign created: " + path + ". Move the root object with W, rotate with E. Size: 1.2 x 0.84 m.", root);
    }

    [MenuItem("GameObject/VR Mechanics/View Selected Safety Sign", false, 36)]
    public static void ViewSelected()
    {
        if (Selection.activeTransform && SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.LookAt(Selection.activeTransform.position,
                Selection.activeTransform.rotation, 1.05f, false);
    }

    static RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }
    static void Label(RectTransform parent, string value, Font font, int size, float px, float py)
    {
        var go = new GameObject("Текст", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.035f, 0.04f, 0.045f, 1);
        label.supportRichText = false;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.text = value;
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(px, py);
        rect.offsetMax = new Vector2(-px, -py);
    }
}
