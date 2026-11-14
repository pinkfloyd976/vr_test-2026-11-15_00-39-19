using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Attach this concrete component to the Crusher button.
[AddComponentMenu("VR Mechanics/ДРОБИЛЬНИК")]
public sealed class CrusherLevelButton : VRMenuRoomButton { }

// Shared implementation lives in this file. Import all three .cs files together.
[RequireComponent(typeof(BoxCollider))]
public abstract class VRMenuRoomButton : MonoBehaviour
{
    [Header("Progress / Порядок прохождения")]
    public VRTrainingStage stage;
    public VRStageProgress progress;
    public Text menuLabel;
    [Header("Destination / Точка в этой же сцене")]
    [Tooltip("Пустой объект на полу комнаты, в месте появления игрока")]
    public Transform destination;
    [Tooltip("Teleportation Provider внутри твоего XR Origin")]
    public TeleportationProvider teleportationProvider;
    [Tooltip("Повернуть игрока в направлении синей оси Z точки назначения")]
    public bool faceDestinationForward = true;
    [Min(0.1f)] public float teleportCooldown = 0.75f;
    [Header("Hands / Точки касания на контроллерах")]
    public Transform leftTouchPoint;
    public Transform rightTouchPoint;
    [Min(0)] public float touchRadius = 0.015f;
    [Min(0)] public float releaseMargin = 0.01f;
    [Min(0.05f)] public float pressHoldTime = 0.18f;
    [Header("Button / Кнопка")]
    public BoxCollider pressArea;
    [Tooltip("Дочерняя видимая часть. Корень кнопки должен оставаться неподвижным")]
    public Transform buttonVisual;
    [Tooltip("Направление движения в локальных осях КОРНЯ кнопки")]
    public Vector3 localPressDirection = Vector3.forward;
    [Min(0)] public float pressTravel = 0.012f;
    [Min(0.001f)] public float pressSpeed = 0.08f;
    [Header("Diagnostics / Только для проверки")]
    [SerializeField] bool touching;
    static float nextTeleportTime;
    Vector3 restPosition;
    float travel, held;
    bool armed, attempted, hasVisual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTeleportState() { nextTeleportTime = 0; }
    protected virtual void Reset()
    {
        pressArea = GetComponent<BoxCollider>();
        pressArea.isTrigger = true;
        if (this is AutoclaveLevelButton) stage = VRTrainingStage.Autoclave;
        else if (this is PumpLevelButton) stage = VRTrainingStage.Pump;
        else if (this is LiquidGlassLevelButton) stage = VRTrainingStage.LiquidGlass;
    }
    protected virtual void Awake()
    {
        if (!pressArea) pressArea = GetComponent<BoxCollider>();
        // The sensing volume must not move with the cap.
        hasVisual = buttonVisual && buttonVisual != transform && buttonVisual.IsChildOf(transform)
            && pressArea.transform != buttonVisual && !pressArea.transform.IsChildOf(buttonVisual);
        if (hasVisual) restPosition = buttonVisual.localPosition;
        else if (buttonVisual) Debug.LogWarning("Menu button: Button Visual должен быть дочерней крышкой, а Press Area — неподвижным коллайдером корня.", this);
    }
    protected virtual void Update()
    {
        RefreshMenuLabel();
        float radius = Mathf.Max(0, touchRadius) + (touching ? Mathf.Max(0, releaseMargin) : 0);
        touching = IsTouching(leftTouchPoint, radius) || IsTouching(rightTouchPoint, radius);
        if (!touching)
        {
            armed = true;
            attempted = false;
            held = 0;
        }
        else if (armed) held += Time.unscaledDeltaTime;
        AnimateCap(touching ? Mathf.Max(0, pressTravel) : 0);
        if (armed && touching && !attempted && held >= Mathf.Max(0.05f, pressHoldTime))
        {
            attempted = true;
            TeleportPlayer();
        }
    }
    bool IsTouching(Transform point, float radius)
    {
        if (!point || !point.gameObject.activeInHierarchy || !pressArea || !pressArea.enabled) return false;
        Vector3 local = pressArea.transform.InverseTransformPoint(point.position) - pressArea.center;
        Vector3 half = pressArea.size * 0.5f;
        Vector3 nearest = new Vector3(Mathf.Clamp(local.x, -half.x, half.x),
            Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
        Vector3 world = pressArea.transform.TransformPoint(nearest + pressArea.center);
        return (point.position - world).sqrMagnitude <= radius * radius;
    }
    void AnimateCap(float target)
    {
        if (!hasVisual) return;
        travel = Mathf.MoveTowards(travel, target, Mathf.Max(0.001f, pressSpeed) * Time.unscaledDeltaTime);
        Vector3 direction = localPressDirection.sqrMagnitude > 0.0001f ? localPressDirection.normalized : Vector3.forward;
        Vector3 worldOffset = transform.TransformDirection(direction).normalized * travel;
        buttonVisual.localPosition = restPosition + buttonVisual.parent.InverseTransformVector(worldOffset);
    }
    public void TeleportPlayer()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextTeleportTime) return;
        if (!progress)
        {
            Debug.LogError("Menu button: назначь общий VRStageProgress в поле Progress.", this);
            return;
        }
        if (!progress.CanEnter(stage))
        {
            Debug.Log("Сначала заверши предыдущий этап: " + VRStageProgress.Title((VRTrainingStage)((int)stage - 1)), this);
            return;
        }
        if (!destination || !teleportationProvider || !teleportationProvider.isActiveAndEnabled)
        {
            Debug.LogError("Menu button: назначь Destination и включенный Teleportation Provider из XR Origin.", this);
            return;
        }
        var mediator = teleportationProvider.mediator;
        if (!mediator || !mediator.isActiveAndEnabled || !mediator.xrOrigin || !mediator.xrOrigin.Camera)
        {
            Debug.LogError("Menu button: проверь Locomotion Mediator, XR Origin и Camera в настройках рига.", this);
            return;
        }
        // Keep the player upright, even if the destination marker is tilted.
        Vector3 forward = Vector3.ProjectOnPlane(destination.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        var request = new TeleportRequest
        {
            destinationPosition = destination.position,
            destinationRotation = Quaternion.LookRotation(forward.normalized, Vector3.up),
            matchOrientation = faceDestinationForward ? MatchOrientation.TargetUpAndForward : MatchOrientation.WorldSpaceUp
        };
        if (teleportationProvider.QueueTeleportRequest(request))
        {
            nextTeleportTime = Time.unscaledTime + Mathf.Max(teleportCooldown, teleportationProvider.delayTime + 0.25f);
        }
    }
    // Retain compatibility with any UnityEvents wired to the previous package.
    public void LoadLevel() { TeleportPlayer(); }
    public void RefreshMenuLabel()
    {
        if (!menuLabel) return;
        string status = !progress ? "НЕ НАСТРОЕНО" : progress.IsCompleted(stage) ? "ПРОЙДЕНО" : progress.CanEnter(stage) ? "ДОСТУПНО" : "ЗАКРЫТО";
        string title = ((int)stage + 1) + ". " + VRStageProgress.Title(stage) + "\n" + status;
        if (menuLabel.text != title) menuLabel.text = title;
        menuLabel.color = progress && progress.CanEnter(stage) ? Color.white : new Color(0.65f, 0.68f, 0.72f);
    }
    [ContextMenu("Test Teleport (Play Mode Only)")]
    void TestTeleport() { TeleportPlayer(); }
    protected virtual void OnDisable()
    {
        touching = false;
        armed = false;
        attempted = false;
        held = travel = 0;
        if (hasVisual && buttonVisual) buttonVisual.localPosition = restPosition;
    }
    protected virtual void OnDrawGizmosSelected()
    {
        BoxCollider area = pressArea ? pressArea : GetComponent<BoxCollider>();
        if (!area) return;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = area.transform.localToWorldMatrix;
        Gizmos.color = touching ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(area.center, area.size);
        Gizmos.matrix = old;
        if (destination)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(destination.position, 0.15f);
            Gizmos.DrawLine(destination.position, destination.position + destination.forward * 0.5f);
        }
    }
}
