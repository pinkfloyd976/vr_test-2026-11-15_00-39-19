#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ВАЖНО: этот файл должен лежать в папке с именем Editor (например Assets/Editor/WrenchNutMenu.cs).
// Добавляет команды в меню правой кнопки в окне Hierarchy.
public static class WrenchNutMenu
{
    const string MakePath = "GameObject/Гайковерт/Сделать выбранные объекты гайками";
    const string RemovePath = "GameObject/Гайковерт/Убрать гайки у выбранных объектов";

    [MenuItem(MakePath, false, 20)]
    static void MakeNuts()
    {
        int added = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<WrenchNut>()) continue;
            var nut = Undo.AddComponent<WrenchNut>(go);
            nut.nutVisual = go.transform;
            added++;
        }
        Debug.Log("Гайковерт: объектов сделано гайками: " + added +
                  ". Выдели их все сразу и настрой размер/ось в инспекторе (настройки применятся ко всем).");
    }

    [MenuItem(MakePath, true)]
    static bool ValidateMake() => Selection.gameObjects.Length > 0;

    [MenuItem(RemovePath, false, 21)]
    static void RemoveNuts()
    {
        int removed = 0;
        foreach (var go in Selection.gameObjects)
        {
            var nut = go.GetComponent<WrenchNut>();
            if (!nut) continue;
            Undo.DestroyObjectImmediate(nut);
            removed++;
        }
        Debug.Log("Гайковерт: убрано гаек: " + removed);
    }

    [MenuItem(RemovePath, true)]
    static bool ValidateRemove() => Selection.gameObjects.Length > 0;
}
#endif
