using UnityEngine;
using UnityEngine.Events;

// Root local +Y points OUT of the panel. Surface is the unpressed top face.
public class VRPressButton : MonoBehaviour
{
    public Transform cap;
    public Transform surface;
    public Transform[] pushers;
    public float pusherRadius = 0.012f;
    public float buttonRadius = 0.045f;
    public float travel = 0.025f;
    public float returnSpeed = 0.12f;
    public UnityEvent onPressed = new UnityEvent();
    public UnityEvent onReleased = new UnityEvent();
    Vector3 restPosition;
    float depth;
    bool pressed;

    void Awake() { if (cap) restPosition = cap.localPosition; }
    void Update()
    {
        if (!cap || !surface || travel <= 0f) return;
        float target = 0f;
        if (pushers != null) foreach (Transform pusher in pushers)
        {
            if (!pusher || !pusher.gameObject.activeInHierarchy) continue;
            Vector3 relative = pusher.position - surface.position;
            float height = Vector3.Dot(relative, transform.up);
            float lateral = Vector3.ProjectOnPlane(relative, transform.up).magnitude;
            if (lateral <= buttonRadius && height >= -travel - pusherRadius)
                target = Mathf.Max(target, Mathf.Clamp(pusherRadius - height, 0f, travel));
        }
        depth = target > depth ? target : Mathf.MoveTowards(depth, target, returnSpeed * Time.deltaTime);
        cap.localPosition = restPosition + cap.parent.InverseTransformVector(-transform.up * depth);
        if (!pressed && depth >= travel * 0.9f) { pressed = true; onPressed.Invoke(); }
        else if (pressed && depth <= travel * 0.3f) { pressed = false; onReleased.Invoke(); }
    }
    void OnDisable()
    {
        if (cap) cap.localPosition = restPosition;
        depth = 0f;
        if (pressed) { pressed = false; onReleased.Invoke(); }
    }
}
