using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Повесь на любой объект с Collider (например, куб). Когда игрок касается его
/// контроллером, он телепортируется в точку Destination (в той же сцене).
/// Контроллеры и TeleportationProvider ищутся автоматически.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TouchTeleportButton : MonoBehaviour
{
    [Header("Куда телепортировать")]
    [Tooltip("Пустой объект НА УРОВНЕ ПОЛА. Синяя стрелка (Z) = куда будет смотреть игрок.")]
    public Transform destination;
    public bool matchOrientation = true;

    [Header("Касание")]
    [Tooltip("Точка касания находится на этом расстоянии перед контроллером (м).")]
    public float touchPointForwardOffset = 0.05f;
    [Min(0.001f)] public float touchRadius = 0.04f;
    [Min(0f)] public float cooldown = 1.5f;

    [Header("Авто-поиск (можно оставить пустым)")]
    public XROrigin xrOrigin;
    public TeleportationProvider teleportationProvider;
    public Transform leftController;
    public Transform rightController;

    Collider area;
    float lastTime = -100f;
    bool wasTouching;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        area = GetComponent<Collider>();
        // Триггер, чтобы Character Controller игрока не упирался в предмет
        area.isTrigger = true;

        if (!xrOrigin) xrOrigin = FindFirstObjectByType<XROrigin>();
        if (!teleportationProvider) teleportationProvider = FindFirstObjectByType<TeleportationProvider>();

        if (xrOrigin)
        {
            if (!leftController) leftController = FindByName(xrOrigin.transform, "Left Controller");
            if (!rightController) rightController = FindByName(xrOrigin.transform, "Right Controller");
        }

        if (!destination) Debug.LogWarning("TouchTeleportButton: не указан Destination", this);
        if (!teleportationProvider) Debug.LogWarning("TouchTeleportButton: не найден TeleportationProvider (объект Teleportation в Locomotion)", this);
        if (!leftController && !rightController) Debug.LogWarning("TouchTeleportButton: не найдены контроллеры, укажи их вручную", this);
    }

    static Transform FindByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    void Update()
    {
        bool touching = IsTouching(leftController) || IsTouching(rightController);
        if (touching && !wasTouching) TryTeleport();
        wasTouching = touching;
    }

    bool IsTouching(Transform controller)
    {
        if (!controller || !controller.gameObject.activeInHierarchy) return false;
        Vector3 point = controller.TransformPoint(0f, 0f, touchPointForwardOffset);
        Vector3 closest = area.ClosestPoint(point);
        return (closest - point).sqrMagnitude <= touchRadius * touchRadius;
    }

    [ContextMenu("Test Teleport (Play Mode Only)")]
    public void TryTeleport()
    {
        if (!destination || !teleportationProvider) return;
        if (Time.time - lastTime < cooldown) return;
        lastTime = Time.time;

        // Берём только поворот вокруг вертикали, наклон Destination игнорируем,
        // чтобы игрок не оказывался "на боку"
        Vector3 flatForward = Vector3.ProjectOnPlane(destination.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        Quaternion uprightRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

        var request = new TeleportRequest
        {
            destinationPosition = destination.position,
            destinationRotation = uprightRotation,
            matchOrientation = matchOrientation ? MatchOrientation.TargetUpAndForward : MatchOrientation.None
        };
        teleportationProvider.QueueTeleportRequest(request);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var c in new[] { leftController, rightController })
            if (c) Gizmos.DrawWireSphere(c.TransformPoint(0f, 0f, touchPointForwardOffset), touchRadius);

        if (destination)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, destination.position);
            Gizmos.DrawWireSphere(destination.position, 0.2f);
            Gizmos.DrawRay(destination.position, destination.forward * 0.5f);
        }
    }
}
