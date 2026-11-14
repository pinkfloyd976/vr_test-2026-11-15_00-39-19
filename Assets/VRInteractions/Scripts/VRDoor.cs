using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody), typeof(HingeJoint), typeof(XRSimpleInteractable))]
public class VRDoor : MonoBehaviour
{
    public Transform handle;
    public float stiffness = 600f;
    public float damping = 45f;
    public float maxForce = 180f;
    public float releaseDistance = 0.6f;
    Rigidbody body;
    XRSimpleInteractable interactable;
    Transform hand;
    Vector3 offset;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void FixedUpdate()
    {
        if (!handle || !interactable.isSelected) { hand = null; return; }
        var interactor = interactable.interactorsSelecting[0];
        Transform current = interactor.GetAttachTransform(interactable);
        if (current != hand) { hand = current; offset = handle.position - hand.position; }
        Vector3 error = hand.position + offset - handle.position;
        if (error.magnitude > releaseDistance)
        {
            interactable.interactionManager.SelectExit(interactor, interactable);
            hand = null;
            return;
        }
        Vector3 force = stiffness * error - damping * body.GetPointVelocity(handle.position);
        body.AddForceAtPosition(Vector3.ClampMagnitude(force, maxForce), handle.position, ForceMode.Force);
    }
    void OnDisable() => hand = null;
}
