using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreateVRTaskHUD
{
    [MenuItem("GameObject/VR Mechanics/Task HUD", false, 20)]
    public static void Create()
    {
        Camera camera = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Camera>() : null;
        if (!camera) camera = Camera.main;
        if (!camera)
        {
            EditorUtility.DisplayDialog("VR Task HUD", "Сначала добавь XR Origin. Выдели его Main Camera в Hierarchy и повтори команду.", "OK");
            return;
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Shader shader = Shader.Find("VRTaskHUD/AlwaysVisible");
        if (!shader)
        {
            EditorUtility.DisplayDialog("VR Task HUD", "Не найден шейдер. Импортируй папку VRTaskHUDKit целиком и дождись компиляции.", "OK");
            return;
        }
        const string materialPath = "Assets/VRTaskHUD_AlwaysVisible.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var root = new GameObject("VR_TaskHUD", typeof(RectTransform), typeof(Canvas), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Create VR Task HUD");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30000;
        canvas.worldCamera = camera;
        var image = root.GetComponent<Image>();
        image.material = material;
        image.raycastTarget = false;
        var hud = root.AddComponent<VRTaskHUD>();
        hud.headCamera = camera;
        hud.font = font;
        hud.panel = root.GetComponent<RectTransform>();
        hud.background = image;
        hud.taskHeading = Label(root, "Tasks Title", font, material);
        hud.taskBody = Label(root, "Tasks", font, material);
        hud.safetyHeading = Label(root, "Safety Title", font, material);
        hud.safetyBody = Label(root, "Safety", font, material);
        hud.Refresh();
        root.transform.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(
            hud.topLeftOffset.x, hud.topLeftOffset.y, hud.distance)), camera.transform.rotation);
        Selection.activeGameObject = root;
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
    }

    static Text Label(GameObject root, string name, Font font, Material material)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(root.transform, false);
        var label = child.GetComponent<Text>();
        label.font = font;
        label.material = material;
        label.raycastTarget = false;
        return label;
    }
}
