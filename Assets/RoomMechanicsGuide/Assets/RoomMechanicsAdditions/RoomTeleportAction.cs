using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Wire VRPanelTouchButton.onPressed to Teleport(). This component does not sense touch itself.
[DisallowMultipleComponent]
public class RoomTeleportAction : MonoBehaviour
{
    public Transform destination;
    public TeleportationProvider teleportationProvider;
    public bool faceDestinationForward = true;
    [Min(0.1f)] public float cooldown = 1f;
    static float nextAllowedTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCooldown() => nextAllowedTime = 0f;

    public void Teleport()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextAllowedTime) return;
        if (!destination || !teleportationProvider || !teleportationProvider.isActiveAndEnabled ||
            !teleportationProvider.mediator || !teleportationProvider.mediator.isActiveAndEnabled ||
            !teleportationProvider.mediator.xrOrigin || !teleportationProvider.mediator.xrOrigin.Camera)
        {
            Debug.LogWarning("RoomTeleportAction: assign Destination and the active rig's Teleportation Provider; check its Mediator and XR Origin.", this);
            return;
        }
        Vector3 forward = Vector3.ProjectOnPlane(destination.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        var request = new TeleportRequest
        {
            destinationPosition = destination.position,
            destinationRotation = Quaternion.LookRotation(forward.normalized, Vector3.up),
            matchOrientation = faceDestinationForward ? MatchOrientation.TargetUpAndForward : MatchOrientation.WorldSpaceUp
        };
        if (teleportationProvider.QueueTeleportRequest(request))
            nextAllowedTime = Time.unscaledTime + Mathf.Max(0.1f, cooldown);
    }
}
