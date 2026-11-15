using UnityEngine;
using UnityEngine.Events;

public class VRButton : MonoBehaviour
{
    public enum ScriptAction { Включить, Выключить, Переключить, Ничего }

    [Header("Движение кнопки (в локальных координатах)")]
    public Vector3 pressOffset = new Vector3(0f, 0f, -0.02f); // куда отъезжает
    public float speed = 12f;

    [Header("Кто может нажать")]
    [Tooltip("Оставь пустым, чтобы нажимало что угодно с коллайдером")]
    public string controllerTag = "";

    [Header("Какой скрипт запускать / выключать")]
    public MonoBehaviour targetScript;
    public ScriptAction action = ScriptAction.Включить;

    [Header("Доп. события (необязательно)")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    private Vector3 startPos;
    private Vector3 targetPos;
    private int touchCount = 0;

    void Start()
    {
        startPos = transform.localPosition;
        targetPos = startPos;
    }

    void Update()
    {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition, targetPos, Time.deltaTime * speed);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValid(other)) return;

        touchCount++;
        if (touchCount == 1) Press();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValid(other)) return;

        touchCount = Mathf.Max(0, touchCount - 1);
        if (touchCount == 0) Release();
    }

    bool IsValid(Collider other)
    {
        return string.IsNullOrEmpty(controllerTag) || other.CompareTag(controllerTag);
    }

    void Press()
    {
        targetPos = startPos + pressOffset;

        if (targetScript != null)
        {
            switch (action)
            {
                case ScriptAction.Включить:    targetScript.enabled = true; break;
                case ScriptAction.Выключить:   targetScript.enabled = false; break;
                case ScriptAction.Переключить: targetScript.enabled = !targetScript.enabled; break;
            }
        }

        onPressed?.Invoke();
    }

    void Release()
    {
        targetPos = startPos;
        onReleased?.Invoke();
    }
}
