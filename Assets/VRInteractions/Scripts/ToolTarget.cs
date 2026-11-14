using UnityEngine;
using UnityEngine.Events;

public class ToolTarget : MonoBehaviour
{
    public string requiredTool = "Screwdriver";
    public float secondsToComplete = 2f;
    public UnityEvent<float> onProgress = new UnityEvent<float>();
    public UnityEvent onCompleted = new UnityEvent();
    float elapsed;
    bool completed;
    public bool Accepts(string kind) => isActiveAndEnabled && !completed && kind == requiredTool;
    public void Apply(float seconds)
    {
        if (completed) return;
        elapsed += Mathf.Max(0f, seconds);
        float value = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, secondsToComplete));
        if (value >= 1f) completed = true;
        onProgress.Invoke(value);
        if (completed) onCompleted.Invoke();
    }
    public void ResetProgress() { elapsed = 0f; completed = false; onProgress.Invoke(0f); }
}
