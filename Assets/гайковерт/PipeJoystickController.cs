using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Управление подвижной частью трубы стиком контроллера, пока пульт в руке.
// Подключение: на XR Grab Interactable пульта
//   Select Entered -> PipeJoystickController.OnGrabbed
//   Select Exited  -> PipeJoystickController.OnReleased
public class PipeJoystickController : MonoBehaviour
{
    public enum ControlMode { Move, Rotate }
    public enum HandSource { Any, Left, Right }

    [Header("Что двигаем")]
    [Tooltip("Подвижная часть трубы (отдельный объект с pivot в точке крепления). Риг не нужен.")]
    public Transform pipePart;
    [Tooltip("Move — труба смещается вправо/влево/вперёд/назад. Rotate — труба поворачивается (влево-вправо / вверх-вниз).")]
    public ControlMode mode = ControlMode.Move;

    [Header("Ввод: стик контроллера (действие типа Vector2)")]
    public InputActionProperty leftStick;
    public InputActionProperty rightStick;
    [Tooltip("Any = работает стик той руки, которая отклонена сильнее. Left/Right = только выбранная рука.")]
    public HandSource hand = HandSource.Any;
    [Range(0f, 0.9f)] public float deadZone = 0.15f;
    public bool invertX = false;
    public bool invertY = false;
    [Tooltip("Плавность разгона и остановки. 0 = без сглаживания")]
    [Min(0f)] public float inputSmoothing = 10f;

    [Header("Режим Move")]
    [Tooltip("Скорость, метров в секунду")]
    [Min(0f)] public float moveSpeed = 0.3f;
    [Tooltip("Ось «вправо» в локальных координатах родителя трубы")]
    public Vector3 rightAxis = Vector3.right;
    [Tooltip("Ось «вперёд» в локальных координатах родителя трубы")]
    public Vector3 forwardAxis = Vector3.forward;
    [Tooltip("Предел смещения (мин, макс) от стартовой позиции по оси «вправо», метры")]
    public Vector2 limitRight = new Vector2(-0.5f, 0.5f);
    [Tooltip("Предел смещения (мин, макс) от стартовой позиции по оси «вперёд», метры")]
    public Vector2 limitForward = new Vector2(-0.5f, 0.5f);

    [Header("Режим Rotate")]
    [Tooltip("Скорость поворота, градусов в секунду")]
    [Min(0f)] public float rotateSpeed = 25f;
    [Tooltip("Вокруг какой локальной оси поворот влево-вправо (стик по X)")]
    public Vector3 yawAxis = Vector3.up;
    [Tooltip("Вокруг какой локальной оси наклон вперёд-назад (стик по Y)")]
    public Vector3 pitchAxis = Vector3.right;
    public Vector2 yawLimits = new Vector2(-40f, 40f);
    public Vector2 pitchLimits = new Vector2(-25f, 25f);

    [Header("Пульт в руке")]
    [Tooltip("Включено = труба слушается стик только пока пульт взят в руку")]
    public bool requireGrab = true;
    [Tooltip("Эти компоненты выключаются, пока пульт в руке (например Continuous Move Provider и Turn Provider), " +
             "иначе игрок будет ходить тем же стиком")]
    public Behaviour[] disableWhileHeld;
    [Tooltip("Возвращать трубу на старт при перезапуске после критичной ошибки (режим TeleportToStart)")]
    public bool resetOnRestart = true;

    [Header("События")]
    public UnityEvent onControlStarted = new UnityEvent();
    public UnityEvent onControlStopped = new UnityEvent();

    Vector3 startLocalPos;
    Quaternion startLocalRot;
    float offRight, offForward, yaw, pitch;
    Vector2 smoothed;
    int heldCount;

    void Awake()
    {
        if (!pipePart)
        {
            Debug.LogWarning("PipeJoystickController: не назначена подвижная часть (Pipe Part).", this);
            return;
        }
        startLocalPos = pipePart.localPosition;
        startLocalRot = pipePart.localRotation;
    }

    void OnEnable()
    {
        EnableAction(leftStick);
        EnableAction(rightStick);
        SafetyViolationManager.Restarted += OnRestarted;
    }

    void OnDisable()
    {
        SafetyViolationManager.Restarted -= OnRestarted;
        DisableOwnAction(leftStick);
        DisableOwnAction(rightStick);
        if (heldCount > 0) { heldCount = 0; SetBlocked(false); }
    }

    static void EnableAction(InputActionProperty p)
    {
        var a = p.action;
        if (a != null && !a.enabled) a.Enable();
    }

    // Действия, подключённые по ссылке (из XRI Default Input Actions), не выключаем — ими пользуются другие
    static void DisableOwnAction(InputActionProperty p)
    {
        if (p.reference == null && p.action != null) p.action.Disable();
    }

    // ПОДКЛЮЧИТЬ К Select Entered у XR Grab Interactable пульта
    public void OnGrabbed()
    {
        heldCount++;
        if (heldCount == 1)
        {
            SetBlocked(true);
            onControlStarted.Invoke();
        }
    }

    // ПОДКЛЮЧИТЬ К Select Exited у XR Grab Interactable пульта
    public void OnReleased()
    {
        heldCount = Mathf.Max(0, heldCount - 1);
        if (heldCount == 0)
        {
            SetBlocked(false);
            onControlStopped.Invoke();
        }
    }

    void SetBlocked(bool blocked)
    {
        foreach (var b in disableWhileHeld)
            if (b) b.enabled = !blocked;
    }

    void Update()
    {
        if (!pipePart) return;

        bool active = !requireGrab || heldCount > 0;
        Vector2 target = active ? ReadInput() : Vector2.zero;

        float k = inputSmoothing > 0f ? 1f - Mathf.Exp(-inputSmoothing * Time.deltaTime) : 1f;
        smoothed = Vector2.Lerp(smoothed, target, k);
        if (smoothed.sqrMagnitude < 1e-6f)
        {
            smoothed = Vector2.zero;
            return;
        }

        float dt = Time.deltaTime;
        if (mode == ControlMode.Move)
        {
            offRight = Mathf.Clamp(offRight + smoothed.x * moveSpeed * dt, limitRight.x, limitRight.y);
            offForward = Mathf.Clamp(offForward + smoothed.y * moveSpeed * dt, limitForward.x, limitForward.y);
        }
        else
        {
            yaw = Mathf.Clamp(yaw + smoothed.x * rotateSpeed * dt, yawLimits.x, yawLimits.y);
            pitch = Mathf.Clamp(pitch + smoothed.y * rotateSpeed * dt, pitchLimits.x, pitchLimits.y);
        }
        ApplyPose();
    }

    void ApplyPose()
    {
        if (mode == ControlMode.Move)
        {
            pipePart.localPosition = startLocalPos
                + rightAxis.normalized * offRight
                + forwardAxis.normalized * offForward;
        }
        else
        {
            pipePart.localRotation = startLocalRot
                * Quaternion.AngleAxis(yaw, yawAxis.normalized)
                * Quaternion.AngleAxis(pitch, pitchAxis.normalized);
        }
    }

    Vector2 ReadInput()
    {
        Vector2 l = ReadStick(leftStick);
        Vector2 r = ReadStick(rightStick);

        Vector2 v;
        switch (hand)
        {
            case HandSource.Left: v = l; break;
            case HandSource.Right: v = r; break;
            default: v = l.sqrMagnitude >= r.sqrMagnitude ? l : r; break;
        }

        float m = v.magnitude;
        if (m < deadZone) return Vector2.zero;
        v = v.normalized * Mathf.Clamp01((m - deadZone) / (1f - deadZone));

        if (invertX) v.x = -v.x;
        if (invertY) v.y = -v.y;
        return v;
    }

    static Vector2 ReadStick(InputActionProperty p)
    {
        var a = p.action;
        return a != null ? a.ReadValue<Vector2>() : Vector2.zero;
    }

    // Вернуть трубу на стартовое место
    public void ResetPose()
    {
        offRight = offForward = yaw = pitch = 0f;
        smoothed = Vector2.zero;
        if (!pipePart) return;
        pipePart.localPosition = startLocalPos;
        pipePart.localRotation = startLocalRot;
    }

    void OnRestarted()
    {
        if (resetOnRestart) ResetPose();
    }
}
