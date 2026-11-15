using UnityEngine;

public class SimpleObjectRotation : MonoBehaviour
{
    public enum RotationAxis { X, Y, Z }

    [Header("Настройки вращения")]
    public RotationAxis axis = RotationAxis.Y;

    [Tooltip("Градусов в секунду. Отрицательное значение меняет направление.")]
    public float speed = 30f;

    public Space rotationSpace = Space.Self;

    [Header("Вращаться сразу после запуска игры")]
    public bool rotateOnStart = false;

    [Header("Текущее состояние")]
    [SerializeField] private bool isRotating;

    private void Start()
    {
        if (rotateOnStart)
            isRotating = true;
    }

    private void Update()
    {
        if (!isRotating)
            return;

        Vector3 direction;

        switch (axis)
        {
            case RotationAxis.X:
                direction = Vector3.right;
                break;
            case RotationAxis.Z:
                direction = Vector3.forward;
                break;
            default:
                direction = Vector3.up;
                break;
        }

        transform.Rotate(direction, speed * Time.deltaTime, rotationSpace);
    }

    public void StartRotation()
    {
        isRotating = true;
    }

    public void StopRotation()
    {
        isRotating = false;
    }

    public void ToggleRotation()
    {
        isRotating = !isRotating;
    }

    private void OnDisable()
    {
        isRotating = false;
    }
}
