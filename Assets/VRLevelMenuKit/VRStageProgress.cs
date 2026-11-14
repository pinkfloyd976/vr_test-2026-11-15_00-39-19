using UnityEngine;
using UnityEngine.Events;

public enum VRTrainingStage { Crusher, Autoclave, Pump, LiquidGlass }

[DisallowMultipleComponent]
public class VRStageProgress : MonoBehaviour
{
    [Header("Testing / Временный свободный доступ")]
    [Tooltip("Включено: все четыре телепорта доступны без прохождения заданий. Выключи, чтобы вернуть порядок.")]
    public bool unlockAllTeleportsForTesting = true;
    [SerializeField, Range(0, 4)] int completedStages;
    public UnityEvent onProgressChanged = new UnityEvent();
    public UnityEvent onAllCompleted = new UnityEvent();
    public int CompletedStages => completedStages;

    void Awake() { completedStages = 0; }
    public bool CanEnter(VRTrainingStage stage) => (int)stage >= 0 && (int)stage < 4 && (unlockAllTeleportsForTesting || (int)stage <= completedStages);
    public bool IsCompleted(VRTrainingStage stage) => (int)stage >= 0 && (int)stage < completedStages;

    public static string Title(VRTrainingStage stage)
    {
        switch (stage)
        {
            case VRTrainingStage.Crusher: return "ДРОБИЛЬНИК";
            case VRTrainingStage.Autoclave: return "АВТОКЛАВ";
            case VRTrainingStage.Pump: return "НАСОС";
            case VRTrainingStage.LiquidGlass: return "РАЗГРУЗКА ЖИДКОГО СТЕКЛА";
            default: return "НЕИЗВЕСТНЫЙ ЭТАП";
        }
    }

    // Called ONLY by the level's validated success condition. Numbers are 1..4.
    public void CompleteStage(int stageNumber)
    {
        if (!Application.isPlaying || stageNumber != completedStages + 1 || stageNumber > 4) return;
        completedStages++;
        onProgressChanged.Invoke();
        if (completedStages == 4) onAllCompleted.Invoke();
    }
    public void CompleteCrusher() => CompleteStage(1);
    public void CompleteAutoclave() => CompleteStage(2);
    public void CompletePump() => CompleteStage(3);
    public void CompleteLiquidGlass() => CompleteStage(4);
    public void ResetProgress()
    {
        completedStages = 0;
        onProgressChanged.Invoke();
    }
    [ContextMenu("TEST ONLY - Complete current stage (Play Mode)")]
    void TestCompleteCurrentStage() { CompleteStage(completedStages + 1); }
}
