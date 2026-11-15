using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Повесь на ЛЮБОЙ объект (болт, гайка, любая деталь) — и он станет гайкой для VRImpactWrench.
// Быстрее всего: выдели объекты в Hierarchy -> правый клик -> Гайковерт -> Сделать выбранные объекты гайками.
//
// Как работает: когда головка гайковёрта надета на гайку и зажат триггер, гайка
// вращается и выезжает наружу (откручивание) или заезжает внутрь (затягивание).
public class WrenchNut : MonoBehaviour
{
    public enum Axis { PlusX, MinusX, PlusY, MinusY, PlusZ, MinusZ }

    public static readonly List<WrenchNut> ActiveNuts = new List<WrenchNut>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { ActiveNuts.Clear(); }

    [Header("Что крутится")]
    [Tooltip("Объект, который вращается и выезжает. Пусто = этот же объект.")]
    public Transform nutVisual;

    [Header("Размер под головку")]
    [Tooltip("Должен совпадать с Socket Size MM у гайковёрта")]
    public int socketSizeMM = 24;

    [Header("Направление «наружу»")]
    [Tooltip("Какая локальная ось объекта смотрит ОТ панели К ИГРОКУ (вдоль болта наружу). " +
             "Выбери объект — в окне Scene зелёная линия должна идти от панели к игроку.")]
    public Axis outwardAxis = Axis.PlusZ;
    [Tooltip("Включи, если ось болта проходит через точку-центр объекта (pivot), а не через центр его габаритов")]
    public bool usePivotAsAxisCenter = false;

    [Header("Резьба")]
    [Tooltip("На сколько метров гайка выезжает за один оборот (0.002 = 2 мм)")]
    [Min(0.0001f)] public float pitch = 0.002f;
    [Tooltip("Сколько оборотов от «затянута» до «полностью откручена»")]
    [Min(0.1f)] public float maxTurns = 8f;
    [Tooltip("Сколько оборотов уже откручено в начале (0 = гайка затянута)")]
    [Min(0f)] public float startTurns = 0f;

    [Header("События (можно подключить звук, счётчик ошибок и т.д.)")]
    public UnityEvent onFullyTightened = new UnityEvent();
    public UnityEvent onFullyLoosened = new UnityEvent();

    [Header("Диагностика")]
    [SerializeField] float turns; // 0 = затянута, maxTurns = откручена

    Vector3 pivotLocal;
    Vector3 contactLocal;
    bool wasTight;
    bool wasLoose;

    public Transform Visual => nutVisual ? nutVisual : transform;
    public Vector3 OutwardAxis => Visual.TransformDirection(AxisVector(outwardAxis)).normalized;
    public Vector3 ContactPosition => Visual.TransformPoint(contactLocal);
    public float LoosenedTurns => turns;
    public bool IsTight => turns <= 0.0001f;
    public bool IsLoose => turns >= maxTurns - 0.0001f;

    static Vector3 AxisVector(Axis a)
    {
        switch (a)
        {
            case Axis.PlusX: return Vector3.right;
            case Axis.MinusX: return Vector3.left;
            case Axis.PlusY: return Vector3.up;
            case Axis.MinusY: return Vector3.down;
            case Axis.MinusZ: return Vector3.back;
            default: return Vector3.forward;
        }
    }

    void Awake()
    {
        ComputeWorldPoints(out Vector3 pivotW, out Vector3 contactW);
        pivotLocal = Visual.InverseTransformPoint(pivotW);
        contactLocal = Visual.InverseTransformPoint(contactW);
    }

    void Start()
    {
        float start = Mathf.Clamp(startTurns, 0f, maxTurns);
        if (start > 0f)
        {
            Move(start);
            turns = start;
        }
        wasTight = IsTight;
        wasLoose = IsLoose;
    }

    void OnEnable()
    {
        if (!ActiveNuts.Contains(this)) ActiveNuts.Add(this);
    }

    void OnDisable()
    {
        ActiveNuts.Remove(this);
    }

    // Вызывается гайковёртом. Отрицательное значение = затягивание, положительное = откручивание.
    // Возвращает, сколько оборотов реально сделано (0, если гайка упёрлась в предел).
    public float Drive(float deltaTurns)
    {
        float target = Mathf.Clamp(turns + deltaTurns, 0f, maxTurns);
        float actual = target - turns;
        if (Mathf.Abs(actual) < 1e-7f) return 0f;

        Move(actual);
        turns = target;

        bool tight = IsTight;
        bool loose = IsLoose;
        if (tight && !wasTight) onFullyTightened.Invoke();
        if (loose && !wasLoose) onFullyLoosened.Invoke();
        wasTight = tight;
        wasLoose = loose;

        return actual;
    }

    void Move(float actualTurns)
    {
        Transform vis = Visual;
        Vector3 axis = OutwardAxis;
        Vector3 pivotWorld = vis.TransformPoint(pivotLocal);

        // Затягивание = по часовой стрелке, если смотреть снаружи (от игрока).
        vis.RotateAround(pivotWorld, axis, -actualTurns * 360f);
        // Откручивание выдвигает гайку наружу, затягивание задвигает внутрь.
        vis.position += axis * (actualTurns * pitch);
    }

    void ComputeWorldPoints(out Vector3 pivotW, out Vector3 contactW)
    {
        Transform vis = Visual;
        Vector3 outW = vis.TransformDirection(AxisVector(outwardAxis)).normalized;

        bool has = false;
        Bounds b = new Bounds();
        foreach (var r in vis.GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!has)
        {
            pivotW = vis.position;
            contactW = vis.position;
            return;
        }

        Vector3 e = b.extents;
        float half = Mathf.Abs(outW.x) * e.x + Mathf.Abs(outW.y) * e.y + Mathf.Abs(outW.z) * e.z;
        pivotW = usePivotAsAxisCenter ? vis.position : b.center;
        contactW = pivotW + outW * half; // центр верхней грани гайки
    }

    void OnDrawGizmosSelected()
    {
        Vector3 p, c;
        if (Application.isPlaying)
        {
            p = Visual.TransformPoint(pivotLocal);
            c = Visual.TransformPoint(contactLocal);
        }
        else
        {
            ComputeWorldPoints(out p, out c);
        }

        Vector3 dir = (c - p).normalized;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(p, c + dir * 0.05f);        // зелёная линия: «наружу» (к игроку)
        Gizmos.DrawWireSphere(c, 0.008f);           // точка, куда должна попасть головка
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p, 0.004f);           // центр вращения
    }
}
