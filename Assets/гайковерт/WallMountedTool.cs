using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Повесь на ImpactWrench (рядом с XR Grab Interactable).
/// - Гайковерт висит на стене в точке Mount Point.
/// - Пока контроллер рядом (hover), он подсвечивается белым контуром; в руке контура нет.
/// - Отпустили в воздухе: падает (гравитация). Если никто не взял за Return Delay секунд, возвращается на стену.
/// - Отпустили в зоне Snap Radius вокруг Mount Point: плавно встаёт на место.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class WallMountedTool : MonoBehaviour
{
    public enum ToolState { Docked, Held, Free }

    [Header("Место на стене")]
    [Tooltip("Пустой объект. Позиция и поворот гайковерта, когда он висит на стене. Его можно двигать и вращать в сцене.")]
    public Transform mountPoint;
    [Tooltip("Радиус зоны (м) вокруг Mount Point. Отпустил гайковерт внутри неё: он встаёт на место.")]
    [Min(0.01f)] public float snapRadius = 0.12f;
    [Min(0.01f)] public float snapDuration = 0.15f;

    [Header("Падение и возврат")]
    [Tooltip("Через сколько секунд после падения гайковерт вернётся на стену, если его не подняли.")]
    [Min(0.5f)] public float returnDelay = 5f;
    [Tooltip("Если гайковерт провалился ниже Mount Point на столько метров, он сразу возвращается.")]
    [Min(1f)] public float fallLimit = 30f;

    [Header("Подсветка")]
    [Tooltip("Материал контура (URP/Unlit, белый, Render Face = Back). Создаётся командой Setup в меню компонента.")]
    public Material outlineMaterial;
    [Tooltip("Толщина контура в метрах.")]
    [Min(0.0005f)] public float outlineWidth = 0.008f;
    [Tooltip("Подсвечивать, только если контроллер ближе этого расстояния (м). 0 = подсвечивать при любом наведении.")]
    [Min(0f)] public float highlightDistance = 0.4f;
    [Tooltip("Какие рендереры обводить. Пусто = все MeshRenderer внутри гайковерта.")]
    public Renderer[] outlineRenderers;
    [Tooltip("Мелкие детали (их самый большой размер меньше этого значения, м) не обводятся — иначе они раздуваются в пятна.")]
    [Min(0f)] public float minOutlinePartSize = 0.03f;
    [Tooltip("Максимальное увеличение копии-контура по любой оси.")]
    [Min(1f)] public float maxOutlineScale = 1.6f;

    [Header("Диагностика")]
    [SerializeField] ToolState state = ToolState.Docked;
    [SerializeField] float freeTimer;

    const string ShellName = "OutlineShell";

    XRGrabInteractable grab;
    Rigidbody body;
    RigidbodyInterpolation originalInterpolation;
    readonly List<GameObject> shells = new List<GameObject>();
    bool outlineOn;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        originalInterpolation = body.interpolation == RigidbodyInterpolation.None
            ? RigidbodyInterpolation.Interpolate : body.interpolation;

        if (!mountPoint)
        {
            var go = new GameObject("WrenchWallMount (auto)");
            go.transform.SetPositionAndRotation(transform.position, transform.rotation);
            mountPoint = go.transform;
            Debug.LogWarning("WallMountedTool: Mount Point не задан, создан автоматически в текущей позиции гайковерта. " +
                             "Выбери компонент → меню ⋮ → Setup, чтобы создать зону заранее и подвинуть её.", this);
        }
        else if (mountPoint.IsChildOf(transform))
        {
            Debug.LogWarning("WallMountedTool: Mount Point не должен быть дочерним объектом гайковерта. Он отвязан.", this);
            mountPoint.SetParent(null, true);
        }

        BuildOutline();
    }

    void Start() => DockImmediate();

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        StopAllCoroutines();
        SetOutline(false);
    }

    // ---------------- Захват / отпускание ----------------

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        StopAllCoroutines();
        state = ToolState.Held;
        freeTimer = 0f;
        body.interpolation = originalInterpolation;
        SetOutline(false);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (!isActiveAndEnabled) return;
        bool inZone = Vector3.Distance(transform.position, mountPoint.position) <= snapRadius;
        StopAllCoroutines();
        StartCoroutine(AfterRelease(inZone));
    }

    IEnumerator AfterRelease(bool inZone)
    {
        // Даём XRI закончить свою обработку отпускания, потом применяем своё состояние.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (grab.isSelected) yield break;

        if (inZone) yield return SnapToMount();
        else EnterFree();
    }

    // ---------------- Состояния ----------------

    void EnterFree()
    {
        state = ToolState.Free;
        freeTimer = 0f;
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.interpolation = originalInterpolation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    IEnumerator SnapToMount()
    {
        state = ToolState.Docked;
        PrepareKinematic();
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, snapDuration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, mountPoint.position, k),
                Quaternion.Slerp(startRot, mountPoint.rotation, k));
            yield return null;
        }
        DockImmediate();
    }

    void ReturnToMount()
    {
        if (grab.isSelected) return;
        StopAllCoroutines();
        DockImmediate();
    }

    void PrepareKinematic()
    {
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.interpolation = RigidbodyInterpolation.None;
    }

    void DockImmediate()
    {
        PrepareKinematic();
        transform.SetPositionAndRotation(mountPoint.position, mountPoint.rotation);
        body.position = mountPoint.position;
        body.rotation = mountPoint.rotation;
        state = ToolState.Docked;
        freeTimer = 0f;
    }

    // ---------------- Update: подсветка и таймер возврата ----------------

    void Update()
    {
        bool near = IsHandNear();
        SetOutline(near);

        if (state != ToolState.Free || grab.isSelected) return;

        if (transform.position.y < mountPoint.position.y - fallLimit)
        {
            ReturnToMount();
            return;
        }

        // Пока рука тянется к гайковерту, таймер стоит.
        if (!near) freeTimer += Time.deltaTime;
        if (freeTimer >= returnDelay) ReturnToMount();
    }

    bool IsHandNear()
    {
        if (grab.isSelected) return false;
        var hovering = grab.interactorsHovering;
        for (int i = 0; i < hovering.Count; i++)
        {
            var interactor = hovering[i];
            if (interactor == null || interactor is XRSocketInteractor) continue;
            if (highlightDistance <= 0f) return true;
            Transform attach = interactor.GetAttachTransform(grab);
            if (!attach) continue;
            if (DistanceToTool(attach.position) <= highlightDistance) return true;
        }
        return false;
    }

    float DistanceToTool(Vector3 point)
    {
        float best = float.MaxValue;
        var cols = grab.colliders;
        for (int i = 0; i < cols.Count; i++)
        {
            if (!cols[i]) continue;
            float d = (cols[i].bounds.ClosestPoint(point) - point).magnitude;
            if (d < best) best = d;
        }
        return best == float.MaxValue ? Vector3.Distance(point, transform.position) : best;
    }

    // ---------------- Контур ----------------

    void BuildOutline()
    {
        if (!outlineMaterial) outlineMaterial = CreateRuntimeMaterial();
        if (!outlineMaterial)
        {
            Debug.LogWarning("WallMountedTool: нет материала контура, подсветка отключена. " +
                             "Выбери компонент → меню ⋮ → Setup.", this);
            return;
        }

        Renderer[] sources = (outlineRenderers != null && outlineRenderers.Length > 0)
            ? outlineRenderers : GetComponentsInChildren<MeshRenderer>(true);

        foreach (var r in sources)
        {
            if (!r || r.gameObject.name == ShellName) continue;
            var filter = r.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;

            Mesh mesh = filter.sharedMesh;
            Bounds b = mesh.bounds;
            Vector3 lossy = r.transform.lossyScale;
            float maxPartSize = Mathf.Max(Mathf.Abs(b.size.x * lossy.x),
                Mathf.Max(Mathf.Abs(b.size.y * lossy.y), Mathf.Abs(b.size.z * lossy.z)));
            if (maxPartSize < minOutlinePartSize) continue;
            Vector3 scale = Vector3.one;
            for (int axis = 0; axis < 3; axis++)
            {
                float size = Mathf.Abs(b.size[axis] * lossy[axis]);
                scale[axis] = size > 0.00001f ? Mathf.Min(1f + 2f * outlineWidth / size, maxOutlineScale) : 1f;
            }

            var shell = new GameObject(ShellName);
            shell.layer = r.gameObject.layer;
            shell.transform.SetParent(r.transform, false);
            shell.transform.localScale = scale;
            shell.transform.localPosition = Vector3.Scale(b.center, Vector3.one - scale);
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = shell.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.SetActive(false);
            shells.Add(shell);
        }
    }

    void SetOutline(bool on)
    {
        if (on == outlineOn) return;
        outlineOn = on;
        for (int i = 0; i < shells.Count; i++)
            if (shells[i]) shells[i].SetActive(on);
    }

    static Material CreateRuntimeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) return null;
        var m = new Material(shader) { name = "WrenchOutline (runtime)" };
        ConfigureOutlineMaterial(m);
        return m;
    }

    static void ConfigureOutlineMaterial(Material m)
    {
        // Render Face = Back: рисуем только внутреннюю сторону увеличенной копии, получается контур.
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Front);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
    }

    // ---------------- Гизмо: зона видна в окне Scene ----------------

    void OnDrawGizmos()
    {
        if (!mountPoint) return;
        Vector3 p = mountPoint.position;
        Gizmos.color = new Color(0f, 1f, 1f, 0.12f);
        Gizmos.DrawSphere(p, snapRadius);
        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawWireSphere(p, snapRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(p, mountPoint.forward * 0.15f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(p, mountPoint.up * 0.1f);
    }

#if UNITY_EDITOR
    // ---------------- Команды в меню компонента (правый клик по заголовку / три точки) ----------------

    [ContextMenu("Setup: создать зону на стене и материал контура")]
    void EditorSetup()
    {
        if (Application.isPlaying) return;
        Undo.RecordObject(this, "Setup wall mounted tool");

        if (!mountPoint)
        {
            var go = new GameObject("WrenchWallMount");
            go.transform.SetPositionAndRotation(transform.position, transform.rotation);
            Undo.RegisterCreatedObjectUndo(go, "Create wrench mount");
            mountPoint = go.transform;
            Selection.activeGameObject = go;
        }

        if (!outlineMaterial)
        {
            const string folder = "Assets/GeneratedWrenchDock";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "GeneratedWrenchDock");
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
            {
                var m = new Material(shader) { name = "WrenchOutline" };
                ConfigureOutlineMaterial(m);
                AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath(folder + "/WrenchOutline.mat"));
                AssetDatabase.SaveAssets();
                outlineMaterial = m;
            }
            else Debug.LogWarning("WallMountedTool: шейдер Universal Render Pipeline/Unlit не найден.", this);
        }

        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("WallMountedTool: готово. Подвинь объект WrenchWallMount на стену (синяя стрелка = куда смотрит ствол гайковерта).", this);
    }

    [ContextMenu("Зона: поставить в текущую позицию гайковерта")]
    void EditorMountFromWrench()
    {
        if (Application.isPlaying || !mountPoint) return;
        Undo.RecordObject(mountPoint, "Move mount to wrench");
        mountPoint.SetPositionAndRotation(transform.position, transform.rotation);
    }

    [ContextMenu("Гайковерт: переместить в зону (проверка вида)")]
    void EditorWrenchToMount()
    {
        if (Application.isPlaying || !mountPoint) return;
        Undo.RecordObject(transform, "Move wrench to mount");
        transform.SetPositionAndRotation(mountPoint.position, mountPoint.rotation);
    }
#endif
}
