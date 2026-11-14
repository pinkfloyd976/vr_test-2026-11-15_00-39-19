using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreateVRTrainerTitle
{
    const string Folder = "Assets/VRTrainerTitleKit";
    [MenuItem("GameObject/VR Mechanics/VR Trainer Title", false, 39)]
    public static void Create()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Safety Sign", "Сначала останови Play.", "OK");
            return;
        }
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("VR-тренажёр безопасной отгрузки жидкого стекла");
        Undo.RegisterCreatedObjectUndo(root, "Create VR trainer title");
        var canvasObject = new GameObject("Табличка", typeof(RectTransform), typeof(Canvas), typeof(Image));
        canvasObject.transform.SetParent(root.transform, false);
        canvasObject.transform.localScale = Vector3.one * 0.00075f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2000, 640);
        var border = canvasObject.GetComponent<Image>();
        border.color = new Color(0.035f, 0.04f, 0.045f, 1);
        border.raycastTarget = false;
        Color white = new Color(0.98f, 0.98f, 0.96f, 1);
        Color yellow = new Color(1f, 0.79f, 0.04f, 1);
        Panel(rect, "Акцент", 20, 20, 1960, 24, yellow);
        var body = Panel(rect, "Название", 20, 60, 1960, 560, white);
        Label(body, "VR-тренажёр безопасной\nотгрузки жидкого стекла", font, 120, 70, 45);
        if (Camera.main)
        {
            var camera = Camera.main.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            root.transform.SetPositionAndRotation(camera.position + forward * 2f - camera.right * 1.6f, Quaternion.LookRotation(forward, Vector3.up));
        }
        else if (SceneView.lastActiveSceneView)
        {
            root.transform.position = SceneView.lastActiveSceneView.pivot;
        }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "VRTrainerTitleKit");
        if (!AssetDatabase.IsValidFolder(Folder + "/Prefabs")) AssetDatabase.CreateFolder(Folder, "Prefabs");
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Prefabs/VRTrainerTitle.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        ViewSelected();
        Debug.Log("Safety sign created: " + path + ". Move the root object with W, rotate with E. Size: 1.5 x 0.48 m.", root);
    }

    [MenuItem("GameObject/VR Mechanics/View Selected Trainer Title", false, 40)]
    public static void ViewSelected()
    {
        if (Selection.activeTransform && SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.LookAt(Selection.activeTransform.position,
                Selection.activeTransform.rotation, 1.2f, false);
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

