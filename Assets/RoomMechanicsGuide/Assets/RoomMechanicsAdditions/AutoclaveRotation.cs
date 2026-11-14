using UnityEngine;

// Attach to a scale-1 pivot at the centre of the vessel's rotation axis.
[DisallowMultipleComponent]
public class AutoclaveRotation : MonoBehaviour
{
    [Tooltip("Axis in the pivot's local coordinates. Z = along its blue arrow.")]
    public Vector3 localAxis = Vector3.forward;
    [Min(0f)] public float speedDegreesPerSecond = 25f;
    [Min(0.01f)] public float acceleration = 15f;
    [Min(0.01f)] public float deceleration = 10f;
    public bool reverse;
    [SerializeField] bool running;
    [SerializeField] float currentSpeed;
    public bool IsMoving => Mathf.Abs(currentSpeed) > 0.001f;

    public void ToggleRunning() => running = !running;
    public void StartRotation() => running = true;
    public void StopRotation() => running = false;

    void Update()
    {
        float target = running ? Mathf.Max(0f, speedDegreesPerSecond) * (reverse ? -1f : 1f) : 0f;
        float rate = running ? acceleration : deceleration;
        float dt = Time.deltaTime;
        float previous = currentSpeed;
        float difference = Mathf.Abs(target - previous);
        float timeToTarget = difference / Mathf.Max(0.01f, rate);
        float rampTime = Mathf.Min(dt, timeToTarget);
        currentSpeed = Mathf.MoveTowards(previous, target, Mathf.Max(0.01f, rate) * dt);
        // Integrate the ramp and any time remaining at constant speed.
        float angle = (previous + currentSpeed) * 0.5f * rampTime + currentSpeed * (dt - rampTime);
        if (localAxis.sqrMagnitude > 0.000001f)
            transform.Rotate(localAxis.normalized, angle, Space.Self);
    }

    void OnDisable() { running = false; currentSpeed = 0f; }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 axis = transform.TransformDirection(localAxis.normalized);
        Gizmos.DrawLine(transform.position - axis, transform.position + axis);
    }
}
