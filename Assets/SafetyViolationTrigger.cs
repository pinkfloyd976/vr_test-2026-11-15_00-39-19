using UnityEngine;

public class SafetyViolationTrigger : MonoBehaviour
{
    public SafetyViolationManager manager;
    public string description = "Двигатель включён до проверки защитного кожуха";
    [Tooltip("Classification only. Does not restart the scene.")]
    public bool critical;
    public bool countOnlyOnce = true;
    public bool useTriggerZone;
    [Tooltip("For physical trigger zones only. Set Player or a dedicated hand tag to avoid counting random objects.")]
    public string requiredTag = "Player";
    bool used;
    void OnEnable() { SafetyViolationManager.Restarted += ResetUsed; }
    void OnDisable() { SafetyViolationManager.Restarted -= ResetUsed; }
    void ResetUsed() { used = false; }
    public void Report()
    {
        if (countOnlyOnce && used) return;
        var target = manager ? manager : SafetyViolationManager.Instance;
        if (target && target.TryReport(description, critical)) used = true;
    }
    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerZone) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        Report();
    }
}
