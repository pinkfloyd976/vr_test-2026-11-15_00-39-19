using UnityEngine;

public class FlipAndSpin : MonoBehaviour
{
    public enum State { Ожидание, Переворот, Перевёрнут, Вращение, Доворот }

    [Header("Центр вращения")]
    [Tooltip("Пустышка, вокруг которой вращается объект. Если не задана, объект вращается вокруг собственного центра")]
    public Transform pivot;
    [Tooltip("Если пустышка не задана, вращать вокруг геометрического центра модели (а не вокруг её точки Pivot)")]
    public bool useMeshCenter = true;

    [Header("Ось вращения")]
    [Tooltip("Self: ось берётся в локальных координатах пустышки (или объекта, если пустышки нет). World: в мировых")]
    public Vector3 axis = Vector3.up;
    public Space space = Space.Self;

    [Header("Переворот на 180°")]
    public float flipDuration = 0.6f; // секунд на переворот

    [Header("Постоянное вращение")]
    public float spinSpeed = 90f; // градусов в секунду

    [Header("Доворот после остановки")]
    [Tooltip("Чем больше, тем резче тормозит")]
    public float settleSharpness = 3f;
    [Tooltip("Минимальная скорость доворота (градусов в секунду), чтобы не ползло вечно")]
    public float settleMinSpeed = 20f;

    [Header("Текущее состояние (только для просмотра)")]
    public State state = State.Ожидание;

    private Quaternion baseRotation; // исходная ориентация перед началом цикла
    private Vector3 basePosition;    // исходная позиция перед началом цикла
    private Vector3 pivotPosition;   // мировая позиция центра вращения (запоминается на старте)
    private Vector3 worldAxis;       // мировая ось вращения (запоминается на старте)
    private float angle;             // накопленный угол от исходной ориентации
    private float flipTimer;
    private float settleTarget;

    // ВЫЗЫВАТЬ С КНОПКИ ЗАПУСКА (событие On Pressed)
    public void OnButtonPressed()
    {
        switch (state)
        {
            case State.Ожидание:
                StartFlip();
                break;

            case State.Перевёрнут:
                state = State.Вращение;
                break;

            // Во время переворота, вращения и доворота нажатия игнорируются
        }
    }

    // ВЫЗЫВАТЬ С КНОПКИ ОСТАНОВКИ (событие On Pressed)
    public void StopRotation()
    {
        if (state == State.Ожидание || state == State.Доворот) return;

        // ближайшее значение, кратное 360, в сторону вращения
        settleTarget = Mathf.Ceil(angle / 360f) * 360f;
        state = State.Доворот;
    }

    void StartFlip()
    {
        baseRotation = transform.rotation;
        basePosition = transform.position;

        // Запоминаем центр и ось один раз, чтобы они не "уплывали",
        // даже если пустышка является дочерним объектом вращаемого
        Transform center = pivot != null ? pivot : transform;
        pivotPosition = center.position;

        if (pivot == null && useMeshCenter)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                pivotPosition = b.center;
            }
        }

        Vector3 localAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
        worldAxis = (space == Space.Self) ? (center.rotation * localAxis).normalized : localAxis;

        angle = 0f;
        flipTimer = 0f;
        state = State.Переворот;
    }

    void ApplyAngle()
    {
        Quaternion q = Quaternion.AngleAxis(angle, worldAxis);
        transform.position = pivotPosition + q * (basePosition - pivotPosition);
        transform.rotation = q * baseRotation;
    }

    void Update()
    {
        switch (state)
        {
            case State.Переворот:
                flipTimer += Time.deltaTime;
                float t = Mathf.Clamp01(flipTimer / Mathf.Max(0.01f, flipDuration));
                angle = 180f * Mathf.SmoothStep(0f, 1f, t);
                ApplyAngle();
                if (t >= 1f)
                {
                    angle = 180f;
                    ApplyAngle();
                    state = State.Перевёрнут;
                }
                break;

            case State.Вращение:
                angle += spinSpeed * Time.deltaTime;
                ApplyAngle();
                break;

            case State.Доворот:
                float remaining = settleTarget - angle;
                float speed = Mathf.Clamp(remaining * settleSharpness, settleMinSpeed, Mathf.Max(spinSpeed, settleMinSpeed));
                float step = Mathf.Min(speed * Time.deltaTime, remaining);
                angle += step;
                ApplyAngle();

                if (settleTarget - angle <= 0.001f)
                {
                    // полный оборот завершён: возвращаем точно в исходную позицию и ориентацию
                    angle = 0f;
                    transform.SetPositionAndRotation(basePosition, baseRotation);
                    state = State.Ожидание;
                }
                break;
        }
    }
}
