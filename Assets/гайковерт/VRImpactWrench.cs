using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Ударный гайковёрт. Работает и со старыми гайками (VRThreadedNut), и с новыми (WrenchNut).
// Для новой модели: меню компонента (три точки) -> «Автонастройка по модели».
[RequireComponent(typeof(XRGrabInteractable))]
public class VRImpactWrench : MonoBehaviour
{
    public Transform socketTip;
    public Transform rotatingSocket;
    public Renderer directionIndicator;
    public int socketSizeMM = 24;
    [Min(0.01f)] public float turnsPerSecond = 2f;
    [Min(0.001f)] public float engagementDistance = 0.025f;
    [Range(0f, 90f)] public float alignmentAngle = 20f;
    public bool reverse;
    public UnityEvent<bool> onReverseChanged = new UnityEvent<bool>();

    [Header("Вращающаяся головка")]
    [Tooltip("Локальная ось головки (Rotating Socket), направленная из головки В СТОРОНУ ГАЙКИ. " +
             "Для старой модели (0,0,1). Для новой заполняется автонастройкой.")]
    public Vector3 socketSpinAxis = Vector3.forward;
    [Tooltip("Включи, если головка крутится в обратную сторону относительно гайки")]
    public bool invertSocketSpin;

    [Header("Индикатор направления (деталь указывается в поле Direction Indicator выше)")]
    [Tooltip("Яркость свечения панельки: зелёная при закручивании, красная при откручивании. 0 = без свечения")]
    [Min(0f)] public float indicatorGlow = 1.5f;

    [Header("Runtime diagnostics")]
    [SerializeField] bool triggerHeld;
    [SerializeField] VRThreadedNut engagedNut;
    [SerializeField] WrenchNut engagedWrenchNut;
    XRGrabInteractable grab;
    IXRSelectInteractor operatingHand;
    bool previousPrimaryButton;
    bool primaryReady;
    Material[] indicatorMaterials;
    int indicatorState = -1;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        CacheIndicator();
    }
    void OnEnable()
    {
        grab.activated.AddListener(Activate);
        grab.deactivated.AddListener(Deactivate);
        grab.selectExited.AddListener(Release);
        RefreshIndicator();
    }
    void OnDisable()
    {
        grab.activated.RemoveListener(Activate);
        grab.deactivated.RemoveListener(Deactivate);
        grab.selectExited.RemoveListener(Release);
        triggerHeld = false;
        engagedNut = null;
        engagedWrenchNut = null;
        operatingHand = null;
    }
    void Activate(ActivateEventArgs args) => triggerHeld = true;
    void Deactivate(DeactivateEventArgs args) => triggerHeld = false;
    void Release(SelectExitEventArgs args)
    {
        triggerHeld = false;
        engagedNut = null;
        engagedWrenchNut = null;
    }

    [ContextMenu("Toggle Direction")]
    public void ToggleDirection() => SetReverse(!reverse);
    public void SetReverse(bool value)
    {
        reverse = value;
        // Require a new trigger press after changing direction.
        triggerHeld = false;
        RefreshIndicator();
        onReverseChanged.Invoke(reverse);
    }

    void Update()
    {
        IXRSelectInteractor hand = null;
        foreach (var candidate in grab.interactorsSelecting)
            if (!(candidate is XRSocketInteractor)) { hand = candidate; break; }
        if (hand != operatingHand)
        {
            operatingHand = hand;
            primaryReady = false;
            previousPrimaryButton = false;
        }
        if (hand == null || !socketTip)
        {
            triggerHeld = false;
            engagedNut = null;
            engagedWrenchNut = null;
            return;
        }
        ReadDirectionButton(hand);
        RefreshIndicator();
        FindNut(out engagedNut, out engagedWrenchNut);
        if (!triggerHeld) return;

        float desired = turnsPerSecond * Time.deltaTime * (reverse ? 1f : -1f);
        float actual = desired;
        if (engagedNut) actual = engagedNut.Drive(desired);
        else if (engagedWrenchNut) actual = engagedWrenchNut.Drive(desired);

        SpinSocket(actual, engagedNut != null);
    }

    void SpinSocket(float actual, bool legacyNut)
    {
        if (!rotatingSocket || Mathf.Approximately(actual, 0f)) return;

        // Старые гайки (VRThreadedNut) крутили головку с обратным знаком — сохраняем это поведение.
        float sign = legacyNut ? -1f : 1f;
        if (invertSocketSpin) sign = -sign;

        Vector3 axis = socketSpinAxis.sqrMagnitude > 1e-6f ? socketSpinAxis.normalized : Vector3.forward;
        rotatingSocket.Rotate(axis, sign * actual * 360f, Space.Self);
    }

    void ReadDirectionButton(IXRSelectInteractor hand)
    {
        if (hand.handedness == InteractorHandedness.None) return;
        XRNode node = hand.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed)) return;
        if (!primaryReady) { primaryReady = true; previousPrimaryButton = pressed; return; }
        if (pressed && !previousPrimaryButton) ToggleDirection();
        previousPrimaryButton = pressed;
    }

    // Ищет ближайшую подходящую гайку среди старых (VRThreadedNut) и новых (WrenchNut).
    void FindNut(out VRThreadedNut legacy, out WrenchNut modern)
    {
        legacy = null;
        modern = null;
        float best = engagementDistance * engagementDistance;
        float minDot = Mathf.Cos(alignmentAngle * Mathf.Deg2Rad);

        foreach (var nut in VRThreadedNut.ActiveNuts)
        {
            if (!nut || !nut.isActiveAndEnabled || !nut.nutVisual || nut.socketSizeMM != socketSizeMM) continue;
            if (Vector3.Dot(socketTip.forward, -nut.OutwardAxis) < minDot) continue;
            Vector3 offset = socketTip.position - nut.ContactPosition;
            // Reject approaching the nut from behind the panel.
            if (Vector3.Dot(offset, nut.OutwardAxis) < -0.006f) continue;
            float distance = offset.sqrMagnitude;
            if (distance > best) continue;
            best = distance;
            legacy = nut;
            modern = null;
        }

        foreach (var nut in WrenchNut.ActiveNuts)
        {
            if (!nut || !nut.isActiveAndEnabled || nut.socketSizeMM != socketSizeMM) continue;
            if (Vector3.Dot(socketTip.forward, -nut.OutwardAxis) < minDot) continue;
            Vector3 offset = socketTip.position - nut.ContactPosition;
            if (Vector3.Dot(offset, nut.OutwardAxis) < -0.006f) continue;
            float distance = offset.sqrMagnitude;
            if (distance > best) continue;
            best = distance;
            modern = nut;
            legacy = null;
        }
    }

    void CacheIndicator()
    {
        if (!directionIndicator) return;
        // .materials создаёт копии материалов только для этой детали — остальные объекты не затрагиваются
        indicatorMaterials = directionIndicator.materials;
        foreach (var m in indicatorMaterials)
            if (m && m.HasProperty("_EmissionColor")) m.EnableKeyword("_EMISSION");
        indicatorState = -1;
    }

    void RefreshIndicator()
    {
        if (!directionIndicator) return;
        if (indicatorMaterials == null) CacheIndicator();
        if (indicatorMaterials == null) return;

        int state = reverse ? 1 : 0;
        if (state == indicatorState) return;
        indicatorState = state;

        // зелёный = закручивание, красный = откручивание
        Color color = reverse ? new Color(1f, 0.1f, 0.02f) : new Color(0.05f, 0.85f, 0.15f);
        foreach (var m in indicatorMaterials)
        {
            if (!m) continue;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * indicatorGlow);
        }
    }

    void OnDestroy()
    {
        if (indicatorMaterials == null) return;
        foreach (var m in indicatorMaterials) if (m) Destroy(m);
    }

    void OnDrawGizmosSelected()
    {
        if (!socketTip) return;
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(socketTip.position, socketTip.forward * 0.08f); // синяя стрелка: в сторону гайки
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(socketTip.position, engagementDistance);
    }

#if UNITY_EDITOR
    // ---------------- Автонастройка под новую модель (меню компонента: три точки) ----------------

    [ContextMenu("Автонастройка по модели (головка, ось, кончик, коллайдер)")]
    void EditorAutoSetup()
    {
        if (Application.isPlaying) return;

        // 1) Вращающаяся головка: ищем меш с «socket» в имени.
        if (!rotatingSocket)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform || !t.GetComponent<MeshFilter>()) continue;
                if (t.name.ToLowerInvariant().Contains("socket")) { rotatingSocket = t; break; }
            }
        }
        if (!rotatingSocket)
        {
            Debug.LogWarning("VRImpactWrench: не нашёл головку. Перетащи меш головки в поле Rotating Socket и запусти команду снова.", this);
            return;
        }

        Undo.RecordObject(this, "Wrench auto setup");

        // 2) Границы головки и остального корпуса в локальных координатах головки.
        bool hasSock = false, hasBody = false;
        Bounds sock = new Bounds(), body = new Bounds();
        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            bool isSocket = mf.transform == rotatingSocket || mf.transform.IsChildOf(rotatingSocket);
            if (isSocket) EncapsulateMesh(mf.transform, mf.sharedMesh, rotatingSocket, ref hasSock, ref sock);
            else EncapsulateMesh(mf.transform, mf.sharedMesh, rotatingSocket, ref hasBody, ref body);
        }
        if (!hasSock || !hasBody)
        {
            Debug.LogWarning("VRImpactWrench: не удалось определить габариты головки/корпуса.", this);
            return;
        }

        // 3) Ось ствола = самая длинная составляющая вектора «корпус -> головка».
        Vector3 diff = sock.center - body.center;
        int ax = 0;
        float m = Mathf.Abs(diff.x);
        if (Mathf.Abs(diff.y) > m) { ax = 1; m = Mathf.Abs(diff.y); }
        if (Mathf.Abs(diff.z) > m) { ax = 2; }
        float dirSign = diff[ax] >= 0f ? 1f : -1f;

        Vector3 dirLocal = Vector3.zero;
        dirLocal[ax] = dirSign;
        socketSpinAxis = dirLocal;

        // 4) Кончик головки (устье, куда заходит гайка) — на оси вращения, на дальнем краю головки.
        float extent = dirSign > 0f ? sock.max[ax] : -sock.min[ax];
        Vector3 tipWorld = rotatingSocket.TransformPoint(dirLocal * extent);
        Vector3 dirWorld = rotatingSocket.TransformDirection(dirLocal).normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(dirWorld, transform.up)) > 0.99f ? transform.forward : transform.up;
        Quaternion tipRot = Quaternion.LookRotation(dirWorld, up);

        Transform tip = socketTip;
        if (!tip)
        {
            var go = new GameObject("SocketTip");
            Undo.RegisterCreatedObjectUndo(go, "Create SocketTip");
            go.transform.SetParent(transform, false);
            tip = go.transform;
            socketTip = tip;
        }
        if (tip.name == "SocketTip")
        {
            Undo.RecordObject(tip, "Move SocketTip");
            tip.SetPositionAndRotation(tipWorld, tipRot);
        }
        else
        {
            Debug.Log("VRImpactWrench: Socket Tip уже назначен вручную (" + tip.name + ") — не трогаю. " +
                      "Его синяя стрелка должна смотреть в сторону гайки.", this);
        }

        // 5) Коллайдер, чтобы гайковёрт можно было взять в руку.
        if (!GetComponentInChildren<Collider>(true))
        {
            bool hasAll = false;
            Bounds all = new Bounds();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) EncapsulateMesh(mf.transform, mf.sharedMesh, transform, ref hasAll, ref all);
            if (hasAll)
            {
                var box = Undo.AddComponent<BoxCollider>(gameObject);
                box.center = all.center;
                box.size = all.size;
            }
        }

        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("VRImpactWrench: автонастройка готова. Rotating Socket = " + rotatingSocket.name +
                  ", Socket Spin Axis = " + dirLocal + ". Проверь в окне Scene: синяя стрелка SocketTip должна смотреть " +
                  "из головки наружу (в сторону, куда надевается на гайку).", this);
    }

    static void EncapsulateMesh(Transform meshTransform, Mesh mesh, Transform space, ref bool has, ref Bounds result)
    {
        Bounds mb = mesh.bounds;
        Vector3 c = mb.center, e = mb.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            Vector3 p = space.InverseTransformPoint(meshTransform.TransformPoint(corner));
            if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
            else result.Encapsulate(p);
        }
    }
#endif
}
