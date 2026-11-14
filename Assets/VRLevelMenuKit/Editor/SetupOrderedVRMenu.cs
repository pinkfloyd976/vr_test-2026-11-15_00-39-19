using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SetupOrderedVRMenu
{
    [MenuItem("GameObject/VR Mechanics/Setup Ordered Menu and Labels", false, 30)]
    public static void Setup()
    {
        if (Application.isPlaying) return;
        var scene = EditorSceneManager.GetActiveScene();
        var buttons = UnityEngine.Object.FindObjectsByType<VRMenuRoomButton>(FindObjectsInactive.Include)
            .Where(b => b.gameObject.scene == scene).ToArray();
        if (buttons.Length != 4)
        {
            EditorUtility.DisplayDialog("VR Menu", "В активной сцене нужны ровно четыре кнопки с компонентами VRLevelMenuKit. Найдено: " + buttons.Length, "OK");
            return;
        }
        var stages = new VRTrainingStage[4];
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!Identify(buttons[i], out stages[i]))
            {
                EditorUtility.DisplayDialog("VR Menu", "Не удалось определить этап кнопки " + buttons[i].name + ". Назови кнопку ДРОБИЛЬНИК, АВТОКЛАВ, НАСОС или РАЗГРУЗКА ЖИДКОГО СТЕКЛА.", "OK");
                return;
            }
        }
        if (stages.Distinct().Count() != 4)
        {
            EditorUtility.DisplayDialog("VR Menu", "Названия кнопок/точек назначения должны соответствовать четырем разным этапам.", "OK");
            return;
        }
        var managers = UnityEngine.Object.FindObjectsByType<VRStageProgress>(FindObjectsInactive.Include)
            .Where(p => p.gameObject.scene == scene).ToArray();
        if (managers.Length > 1)
        {
            EditorUtility.DisplayDialog("VR Menu", "В сцене несколько VRStageProgress. Оставь один общий менеджер прогресса.", "OK");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup ordered VR menu");
        VRStageProgress progress;
        if (managers.Length == 0)
        {
            var go = new GameObject("VR_StageProgress");
            Undo.RegisterCreatedObjectUndo(go, "Create stage progress");
            progress = Undo.AddComponent<VRStageProgress>(go);
        }
        else progress = managers[0];
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (var pair in buttons.Select((button, index) => new { button, index }))
        {
            var b = pair.button;
            Undo.RecordObject(b, "Configure menu stage");
            b.stage = stages[pair.index];
            b.progress = progress;
            if (!b.menuLabel)
            {
                var go = new GameObject("Название и доступ", typeof(RectTransform), typeof(Canvas), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Create room label");
                go.transform.SetParent(b.transform, false);
                go.transform.localScale = Vector3.one * 0.001f;
                var area = b.pressArea ? b.pressArea : b.GetComponent<BoxCollider>();
                float top = area && area.transform == b.transform ? area.center.y + area.size.y * 0.5f : 0.125f;
                go.transform.localPosition = new Vector3(0, top + 0.19f, -0.05f);
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(660, 280);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.025f, 0.04f, 0.06f, 0.93f);
                bg.raycastTarget = false;
                var textGO = new GameObject("Текст", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGO.transform.SetParent(go.transform, false);
                var text = textGO.GetComponent<Text>();
                text.font = font;
                text.fontSize = 42;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.supportRichText = false;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(20, 12);
                text.rectTransform.offsetMax = new Vector2(-20, -12);
                b.menuLabel = text;
            }
            b.RefreshMenuLabel();
            EditorUtility.SetDirty(b);
        }
        EditorUtility.SetDirty(progress);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = progress.gameObject;
    }

    static bool Identify(VRMenuRoomButton b, out VRTrainingStage stage)
    {
        // Explicit names take precedence: all four buttons may use CrusherLevelButton.
        foreach (string source in new[] { b.name, b.destination ? b.destination.name : "" })
        {
            string s = source.ToLowerInvariant();
            if (s.Contains("дроб") || s.Contains("crusher")) { stage = VRTrainingStage.Crusher; return true; }
            if (s.Contains("автоклав") || s.Contains("autoclave")) { stage = VRTrainingStage.Autoclave; return true; }
            if (s.Contains("насос") || s.Contains("pump")) { stage = VRTrainingStage.Pump; return true; }
            if (s.Contains("стекл") || s.Contains("liquidglass")) { stage = VRTrainingStage.LiquidGlass; return true; }
        }
        stage = default;
        return false;
    }
}
