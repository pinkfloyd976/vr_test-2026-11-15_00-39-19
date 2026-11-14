using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class AlignedHoseSocket : XRSocketInteractor
{
    public string acceptedType = "Water";
    public float maxDistance = 0.04f;
    public float maxAngle = 15f;

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        if (!base.CanSelect(interactable)) return false;
        if (IsSelecting(interactable)) return true;
        var connector = interactable.transform.GetComponent<HoseConnector>();
        if (!connector || !connector.matingPoint || connector.connectorType != acceptedType) return false;
        Transform target = attachTransform ? attachTransform : transform;
        return Vector3.Distance(connector.matingPoint.position, target.position) <= maxDistance
            && Quaternion.Angle(connector.matingPoint.rotation, target.rotation) <= maxAngle;
    }
}
