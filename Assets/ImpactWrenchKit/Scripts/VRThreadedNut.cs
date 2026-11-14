using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Attach to the stationary bolt root. Local +Z points OUT from the panel.
public class VRThreadedNut : MonoBehaviour
{
    public static readonly HashSet<VRThreadedNut> ActiveNuts = new HashSet<VRThreadedNut>();
    public Transform nutVisual;
    public Transform engagementPoint;
    public int socketSizeMM = 24;
    [Min(0.1f)] public float totalTurns = 6f;
    [Min(0.0001f)] public float pitchMetres = 0.005f;
    [Min(0f)] public float turnsFromTight = 6f;
    public Vector3 seatedLocalPosition = new Vector3(0f, 0f, 0.014f);
    public UnityEvent<float> onTightnessChanged = new UnityEvent<float>();
    public UnityEvent onFullyTightened = new UnityEvent();
    public UnityEvent onFullyLoosened = new UnityEvent();
    Quaternion baseRotation;
    public float Tightness => 1f - Mathf.Clamp01(turnsFromTight / Mathf.Max(0.1f, totalTurns));
    public Vector3 OutwardAxis => transform.forward;
    public Vector3 ContactPosition => engagementPoint ? engagementPoint.position : nutVisual.position;

    void Awake()
    {
        if (!nutVisual) { Debug.LogError("Assign Nut Visual.", this); enabled = false; return; }
        baseRotation = nutVisual.localRotation;
        turnsFromTight = Mathf.Clamp(turnsFromTight, 0f, totalTurns);
        ApplyPose();
    }
    void OnEnable() { if (nutVisual) ActiveNuts.Add(this); }
    void OnDisable() => ActiveNuts.Remove(this);

    // Returns the signed turns actually applied (positive = loosening).
    public float Drive(float requestedTurns)
    {
        float previous = turnsFromTight;
        turnsFromTight = Mathf.Clamp(previous + requestedTurns, 0f, totalTurns);
        float applied = turnsFromTight - previous;
        if (Mathf.Abs(applied) < 0.000001f) return 0f;
        ApplyPose();
        onTightnessChanged.Invoke(Tightness);
        if (previous > 0f && turnsFromTight <= 0f) onFullyTightened.Invoke();
        if (previous < totalTurns && turnsFromTight >= totalTurns) onFullyLoosened.Invoke();
        return applied;
    }

    void ApplyPose()
    {
        nutVisual.localPosition = seatedLocalPosition + Vector3.forward * (turnsFromTight * pitchMetres);
        nutVisual.localRotation = Quaternion.AngleAxis(turnsFromTight * 360f, Vector3.forward) * baseRotation;
    }

    public void ResetToLoose() { turnsFromTight = totalTurns; ApplyPose(); onTightnessChanged.Invoke(Tightness); }
}
