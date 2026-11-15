using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class ViolationRecord
{
    public string description;
    public bool critical;
    public float time;
    public int attempt;
}
[Serializable] public class StringEvent : UnityEvent<string> { }

// One manager for the crusher. Critical errors are counted, never auto-reload the shared scene.
public class SafetyViolationManager : MonoBehaviour
{
    public static SafetyViolationManager Instance { get; private set; }
    public static event Action Restarted;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; Restarted = null; }

    [Header("Crusher room")]
    public Camera headCamera;
    public BoxCollider roomArea;
    public string locationName = "ДРОБИТЕЛЬ";
    [Min(0.05f)] public float exitDelay = 0.3f;
    public SafetyReportDisplay reportDisplay;
    [Header("Counting")]
    [Min(0f)] public float duplicateCooldown = 2f;
    public bool saveReportToFile = false;
    [Header("Report messages")]
    [TextArea(2, 4)] public string successMessage = "Отлично! За время нахождения в комнате ошибок не зафиксировано. Продолжайте соблюдать правила безопасности.";
    [TextArea(2, 4)] public string failureMessage = "Следует повторить правила техники безопасности и порядок работы с дробителем, затем повторить упражнение.";
    public StringEvent onMinorViolation = new StringEvent();
    public StringEvent onCriticalViolation = new StringEvent();
    public StringEvent onReportReady = new StringEvent();
    [Header("Runtime diagnostics")]
    [SerializeField] bool playerInsideRoom;
    [SerializeField] bool visitActive;
    [SerializeField] int minorCount;
    [SerializeField] int criticalCount;
    [SerializeField] int currentAttempt = 1;
    readonly List<ViolationRecord> records = new List<ViolationRecord>();
    readonly Dictionary<string, float> lastReport = new Dictionary<string, float>();
    float outsideSince = -1f;
    float sessionStartTime;
    bool enteredOnce;
    public int MinorCount => minorCount;
    public int CriticalCount => criticalCount;
    public int TotalCount => records.Count;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("Use only one SafetyViolationManager for this room.", this);
            enabled = false;
            return;
        }
        Instance = this;
        if (!headCamera) headCamera = Camera.main;
        sessionStartTime = Time.unscaledTime;
    }
    void LateUpdate()
    {
        // Missing/disabled configuration must not produce a false exit report.
        if (!headCamera || !roomArea || !roomArea.enabled || !roomArea.gameObject.activeInHierarchy)
        {
            playerInsideRoom = false;
            outsideSince = -1f;
            return;
        }
        playerInsideRoom = IsInside();
        if (playerInsideRoom)
        {
            if (!enteredOnce) { enteredOnce = true; sessionStartTime = Time.unscaledTime; }
            if (!visitActive && reportDisplay) reportDisplay.HideReport();
            visitActive = true;
            outsideSince = -1f;
        }
        else if (visitActive)
        {
            if (outsideSince < 0f) outsideSince = Time.unscaledTime;
            if (Time.unscaledTime - outsideSince >= Mathf.Max(0.05f, exitDelay))
            {
                visitActive = false;
                outsideSince = -1f;
                FinishSession();
            }
        }
    }
    bool IsInside()
    {
        if (!headCamera || !roomArea || !roomArea.enabled || !roomArea.gameObject.activeInHierarchy) return false;
        Vector3 p = roomArea.transform.InverseTransformPoint(headCamera.transform.position) - roomArea.center;
        Vector3 h = roomArea.size * 0.5f;
        return Mathf.Abs(p.x) <= Mathf.Abs(h.x) && Mathf.Abs(p.y) <= Mathf.Abs(h.y) && Mathf.Abs(p.z) <= Mathf.Abs(h.z);
    }
    public static void Add(string description, bool critical)
    {
        if (Instance) Instance.Report(description, critical);
        else Debug.LogWarning("SafetyViolationManager is missing: " + description);
    }
    public void Report(string description, bool critical) => TryReport(description, critical);
    public bool TryReport(string description, bool critical)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !IsInside()) return false;
        if (!enteredOnce) { enteredOnce = true; sessionStartTime = Time.unscaledTime; }
        visitActive = true;
        description = string.IsNullOrWhiteSpace(description) ? "Нарушение техники безопасности" : description;
        string key = (critical ? "C:" : "M:") + description;
        float now = Time.unscaledTime;
        if (lastReport.TryGetValue(key, out float last) && now - last < duplicateCooldown) return false;
        lastReport[key] = now;
        records.Add(new ViolationRecord { description = description, critical = critical,
            time = now - sessionStartTime, attempt = currentAttempt });
        if (critical) { criticalCount++; onCriticalViolation.Invoke(description); }
        else { minorCount++; onMinorViolation.Invoke(description); }
        return true;
    }
    public string BuildReport()
    {
        return "ИТОГИ ЛОКАЦИИ: " + locationName + "\n\n" +
            "Количество ошибок: " + TotalCount + "\n" +
            "Критических: " + criticalCount + "   Остальных: " + minorCount + "\n\n" +
            (TotalCount == 0 ? successMessage : failureMessage);
    }
    public string FinishSession()
    {
        string report = BuildReport();
        if (reportDisplay) reportDisplay.ShowReportText(report, TotalCount == 0);
        else Debug.LogWarning("Assign Report Display to show the room report in VR.", this);
        onReportReady.Invoke(report);
        if (saveReportToFile)
        {
            try
            {
                var details = new StringBuilder(report).AppendLine().AppendLine();
                foreach (var record in records)
                    details.AppendLine(record.time.ToString("F1") + " с: " + record.description +
                        (record.critical ? " [КРИТИЧЕСКАЯ]" : ""));
                string path = Path.Combine(Application.persistentDataPath,
                    "SafetyReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
                File.WriteAllText(path, details.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("Report save failed: " + e.Message, this); }
        }
        return report;
    }
    // Deliberate reset only. Does not reload or teleport the shared scene.
    public void StartNewSession()
    {
        records.Clear(); lastReport.Clear(); minorCount = criticalCount = 0;
        currentAttempt++;
        sessionStartTime = Time.unscaledTime;
        visitActive = false; enteredOnce = false; outsideSince = -1f;
        if (reportDisplay) reportDisplay.HideReport();
        Restarted?.Invoke();
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
