using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Editor authoring helper. Runtime motion is performed by the EXISTING VRThreadedNut.
[DisallowMultipleComponent]
[RequireComponent(typeof(VRThreadedNut))]
public class AutoclaveNutSetup : MonoBehaviour
{
    public Transform nutVisual;
    public Transform contactPoint;
    [Tooltip("Position of the nut's pivot when fully tightened, not the contact face.")]
    public Transform seatedMarker;
    [Tooltip("Position of the SAME pivot at the maximum permitted loosening.")]
    public Transform looseMarker;
    [Min(0.1f)] public float totalTurns = 6f;
    [Min(1)] public int socketSizeMM = 36;

    [ContextMenu("Apply markers to VRThreadedNut")]
    public void ApplyMarkers()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) { Debug.LogWarning("Stop Play before configuring the nut.", this); return; }
        if (!nutVisual || !contactPoint || !seatedMarker || !looseMarker ||
            nutVisual.parent != transform || !contactPoint.IsChildOf(nutVisual) ||
            seatedMarker.IsChildOf(nutVisual) || looseMarker.IsChildOf(nutVisual))
        {
            Debug.LogError("Assign all four fields. Nut Visual must be a direct child of this root; Contact Point must be inside Nut Visual. Markers must NOT be inside Nut Visual.", this);
            return;
        }
        Vector3 seated = transform.InverseTransformPoint(seatedMarker.position);
        Vector3 loose = transform.InverseTransformPoint(looseMarker.position);
        Vector3 travel = loose - seated;
        if (travel.z <= 0f || new Vector2(travel.x, travel.y).magnitude > Mathf.Max(0.0001f, travel.z * 0.01f))
        {
            Debug.LogError("Loose Marker must be further out along this root's local +Z; both markers must have the same local X/Y.", this);
            return;
        }
        var nut = GetComponent<VRThreadedNut>();
        Undo.RecordObjects(new Object[] { nut, nutVisual }, "Configure autoclave nut");
        nut.nutVisual = nutVisual;
        nut.engagementPoint = contactPoint;
        nut.socketSizeMM = socketSizeMM;
        nut.totalTurns = Mathf.Max(0.1f, totalTurns);
        nut.pitchMetres = travel.z / nut.totalTurns;
        nut.seatedLocalPosition = seated;
        nut.turnsFromTight = 0f;
        nutVisual.localPosition = seated;
        EditorUtility.SetDirty(nut);
        PrefabUtility.RecordPrefabInstancePropertyModifications(nut);
        PrefabUtility.RecordPrefabInstancePropertyModifications(nutVisual);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Nut configured: starts tightened, maximum travel is limited by your markers.", this);
#endif
    }
}
