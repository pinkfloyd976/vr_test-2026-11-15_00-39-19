#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CreateImpactWrenchDemo
{
    [MenuItem("GameObject/VR Mechanics/Impact Wrench Demo", false, 11)]
    static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Fail before creating anything if the runtime scripts were copied into Editor.
        if (typeof(VRImpactWrench).Assembly.GetName().Name.Contains("Editor") ||
            typeof(VRThreadedNut).Assembly.GetName().Name.Contains("Editor"))
        {
            EditorUtility.DisplayDialog("Script location", "Move Scripts outside every Editor folder. Keep only the generator inside Editor.", "OK");
            return;
        }
        const string folder = "Assets/GeneratedImpactWrench";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "GeneratedImpactWrench");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        if (!shader) return;
        Material teal = Material(shader, new Color(0.02f, 0.35f, 0.32f), "Teal", folder);
        Material black = Material(shader, new Color(0.025f, 0.03f, 0.035f), "Black", folder);
        Material metal = Material(shader, new Color(0.55f, 0.57f, 0.6f), "Metal", folder);
        Material panelMaterial = Material(shader, new Color(0.2f, 0.25f, 0.3f), "Panel", folder);
        Mesh nutMesh = HexTube(0.0208f, 0.0105f, 0.016f);
        Mesh socketMesh = HexTube(0.03f, 0.022f, 0.045f);
        Mesh threadMesh = BoltThread();
        AssetDatabase.CreateAsset(nutMesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/Nut.asset"));
        AssetDatabase.CreateAsset(socketMesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/Socket.asset"));
        AssetDatabase.CreateAsset(threadMesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/BoltThread.asset"));

        GameObject stand = new GameObject("WrenchTestStand");
        stand.transform.position = new Vector3(0f, 1.2f, 1.6f);
        Primitive(stand.transform, "Panel", PrimitiveType.Cube, Vector3.zero,
            Quaternion.identity, new Vector3(0.65f, 0.35f, 0.05f), panelMaterial, false);
        for (int i = 0; i < 3; i++)
        {
            GameObject bolt = Child(stand.transform, "Bolt_" + (i + 1));
            bolt.transform.localPosition = new Vector3((i - 1) * 0.20f, 0f, -0.028f);
            bolt.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Primitive(bolt.transform, "BoltShaft", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.035f),
                Quaternion.Euler(90f, 0f, 0f), new Vector3(0.015f, 0.035f, 0.015f), metal, false);
            MeshObject(bolt.transform, "ThreadVisual", threadMesh, metal);
            GameObject nut = MeshObject(bolt.transform, "Nut", nutMesh, metal);
            GameObject contact = Child(nut.transform, "EngagementPoint");
            contact.transform.localPosition = new Vector3(0f, 0f, 0.008f);
            VRThreadedNut logic = bolt.AddComponent<VRThreadedNut>();
            logic.nutVisual = nut.transform;
            logic.engagementPoint = contact.transform;
            logic.socketSizeMM = 36;
            logic.turnsFromTight = i == 0 ? 6f : i == 1 ? 0f : 3f;
            nut.transform.localPosition = logic.seatedLocalPosition + Vector3.forward * logic.turnsFromTight * logic.pitchMetres;
        }

        GameObject tool = new GameObject("ImpactWrench");
        tool.transform.position = new Vector3(-0.4f, 1.1f, 1f);
        GameObject body = Primitive(tool.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.035f),
            Quaternion.Euler(90f, 0f, 0f), new Vector3(0.09f, 0.075f, 0.09f), teal, true);
        GameObject grip = Primitive(tool.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.065f, -0.015f),
            Quaternion.identity, new Vector3(0.038f, 0.13f, 0.05f), black, true);
        Primitive(tool.transform, "Battery", PrimitiveType.Cube, new Vector3(0f, -0.14f, -0.015f),
            Quaternion.identity, new Vector3(0.075f, 0.035f, 0.08f), teal, false);
        Primitive(tool.transform, "Gearbox", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.135f),
            Quaternion.Euler(90f, 0f, 0f), new Vector3(0.07f, 0.025f, 0.07f), metal, false);
        GameObject socket = MeshObject(tool.transform, "RotatingSocket", socketMesh, black);
        socket.transform.localPosition = new Vector3(0f, 0.035f, 0.1825f);
        GameObject tip = Child(tool.transform, "SocketTip");
        tip.transform.localPosition = new Vector3(0f, 0.035f, 0.205f);
        GameObject attach = Child(tool.transform, "GripAttach");
        attach.transform.localPosition = grip.transform.localPosition;
        GameObject indicator = Primitive(tool.transform, "DirectionIndicator", PrimitiveType.Sphere,
            new Vector3(0f, 0.077f, 0f), Quaternion.identity, Vector3.one * 0.018f, metal, false);
        Rigidbody rb = tool.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        XRGrabInteractable grab = tool.AddComponent<XRGrabInteractable>();
        grab.selectMode = InteractableSelectMode.Single;
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.useDynamicAttach = false;
        grab.attachTransform = attach.transform;
        grab.throwOnDetach = false;
        grab.colliders.Clear();
        grab.colliders.Add(grip.GetComponent<Collider>());
        grab.colliders.Add(body.GetComponent<Collider>());
        VRImpactWrench wrench = tool.AddComponent<VRImpactWrench>();
        wrench.socketTip = tip.transform;
        wrench.rotatingSocket = socket.transform;
        wrench.directionIndicator = indicator.GetComponent<Renderer>();
        wrench.socketSizeMM = 36;
        AssetDatabase.SaveAssets();
        Undo.RegisterCreatedObjectUndo(stand, "Create wrench stand");
        Undo.RegisterCreatedObjectUndo(tool, "Create impact wrench");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stand.scene);
        Selection.activeGameObject = tool;
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Wrench demo ready. Grip to hold, Trigger to drive, A (right hand) / X (left hand) to reverse. Green=tighten, orange=loosen. Release and press Trigger after switching.", tool);
    }

    static Material Material(Shader shader, Color color, string name, string folder)
    {
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name == "Metal" ? 0.65f : 0.1f);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat"));
        return material;
    }
    static GameObject Child(Transform parent, string name)
    {
        var result = new GameObject(name);
        result.transform.SetParent(parent, false);
        return result;
    }
    static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var result = Child(parent, name);
        result.AddComponent<MeshFilter>().sharedMesh = mesh;
        result.AddComponent<MeshRenderer>().sharedMaterial = material;
        return result;
    }
    static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position,
        Quaternion rotation, Vector3 scale, Material material, bool keepCollider)
    {
        var result = GameObject.CreatePrimitive(type);
        result.name = name;
        result.transform.SetParent(parent, false);
        result.transform.localPosition = position;
        result.transform.localRotation = rotation;
        result.transform.localScale = scale;
        result.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(result.GetComponent<Collider>());
        else result.GetComponent<Collider>().isTrigger = false;
        return result;
    }

    // Flat-shaded hollow hexagonal prism. +Z is its axis.
    static Mesh BoltThread()
    {
        const int steps = 288, sides = 6;
        var vertices = new Vector3[(steps + 1) * (sides + 1)];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[steps * sides * 6];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float a = t * 12f * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 tangent = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0.005f / (Mathf.PI * 2f * 0.008f)).normalized;
            Vector3 binormal = Vector3.Cross(radial, tangent).normalized;
            Vector3 centre = radial * 0.008f + Vector3.forward * (0.005f + t * 0.06f);
            for (int j = 0; j <= sides; j++)
            {
                float b = j * Mathf.PI * 2f / sides;
                int index = i * (sides + 1) + j;
                normals[index] = radial * Mathf.Cos(b) + binormal * Mathf.Sin(b);
                vertices[index] = centre + normals[index] * 0.0008f;
            }
        }
        int k = 0;
        for (int i = 0; i < steps; i++) for (int j = 0; j < sides; j++)
        {
            int a = i * (sides + 1) + j, b = a + sides + 1;
            triangles[k++] = a; triangles[k++] = b; triangles[k++] = a + 1;
            triangles[k++] = b; triangles[k++] = b + 1; triangles[k++] = a + 1;
        }
        var mesh = new Mesh { name = "BoltThread", vertices = vertices, normals = normals, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh HexTube(float outer, float inner, float length)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f, b = (i + 1) * Mathf.PI / 3f;
            Vector3 u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 v = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
            Vector3 z = Vector3.forward * length * 0.5f;
            Quad(vertices, triangles, u * outer - z, v * outer - z, v * outer + z, u * outer + z);
            Quad(vertices, triangles, v * inner - z, u * inner - z, u * inner + z, v * inner + z);
            Quad(vertices, triangles, u * inner + z, u * outer + z, v * outer + z, v * inner + z);
            Quad(vertices, triangles, v * inner - z, v * outer - z, u * outer - z, u * inner - z);
        }
        var mesh = new Mesh { name = "HollowHex" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }
}
#endif
