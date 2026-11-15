using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class SafetyZone : MonoBehaviour
{
    [SerializeField] MachineState machine;
    [SerializeField] GameObject warningUI;
    [SerializeField] string playerTag = "Player";
    public UnityEvent OnViolation;

    bool playerInside;
    bool violationActive;

    void OnEnable()
    {
        machine.Changed += OnMachineChanged;
        Refresh();
    }

    void OnDisable()
    {
        machine.Changed -= OnMachineChanged;
    }

    void OnMachineChanged(bool isOn)
    {
        Refresh();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = true;
        Refresh();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = false;
        Refresh();
    }

    void Refresh()
    {
        bool violation = machine.IsOn && playerInside;
        warningUI.SetActive(violation);

        if (violation && !violationActive) OnViolation.Invoke();
        violationActive = violation;
    }
}