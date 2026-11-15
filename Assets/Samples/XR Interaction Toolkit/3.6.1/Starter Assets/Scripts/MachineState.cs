using System;
using UnityEngine;

public class MachineState : MonoBehaviour
{
    public bool IsOn { get; private set; }
    public event Action<bool> Changed;

    public void TurnOn() { Set(true); }
    public void TurnOff() { Set(false); }

    void Set(bool value)
    {
        if (IsOn == value) return;
        IsOn = value;
        if (Changed != null) Changed(IsOn);
    }
}