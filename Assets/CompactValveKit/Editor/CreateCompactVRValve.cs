#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Place this file in an Editor folder. VRCompactValve.cs stays outside that folder.
public static class CreateCompactVRValve
{
    [MenuItem("GameObject/VR Mechanics/Compact Valve", false, 10)]
    static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Compact Valve", "Stop Play Mode before creating the valve.", "OK");
            return;
        }
        if (typeof(VRCompactValve).Assembly.GetName().Name.Contains("Editor"))
        {
            EditorUtility.DisplayDialog("Script location", "Keep VRCompactValve.cs outside every Editor folder.", "OK");
            return;
        }
        const float radius = 0.18f;
        const float tube = 0.02f;
        const string folder = "Assets/GeneratedCompactValve";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "GeneratedCompactValve");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        if (!shader) { Debug.LogError("No supported Lit shader found."); return; }
        Material red = MakeMaterial(shader, new Color(0.65f, 0.035f, 0.02f), folder, "ValveRed");
        Material steel = MakeMaterial(shader, new Color(0.13f, 0.15f, 0.17f), folder, "ValveSteel");
        Mesh mesh = Torus(radius, tube, 96, 16);
        AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/ValveRim.asset"));

        GameObject root = new GameObject("CompactValve");
        root.transform.position = new Vector3(0f, 1.2f, 1.2f);
        GameObject pivot = Child(root.transform, "ValvePivot");
        Rigidbody body = pivot.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        XRSimpleInteractable interactable = pivot.AddComponent<XRSimpleInteractable>();
        interactable.selectMode = InteractableSelectMode.Single;
        // Default interaction layer, matching the standard XRI Direct Interactor.
        VRCompactValve valve = pivot.AddComponent<VRCompactValve>();
        valve.localAxis = Vector3.forward;
        valve.rimRadius = radius;
        valve.rimTolerance = 0.055f;
        valve.maxGrabDepth = 0.10f;
        valve.holdSlack = 0.04f;
        valve.minAngle = -720f;
        valve.maxAngle = 720f;
        valve.angle = 0f;

        GameObject rim = Child(pivot.transform, "Rim");
        rim.AddComponent<MeshFilter>().sharedMesh = mesh;
        rim.AddComponent<MeshRenderer>().sharedMaterial = red;
        GameObject zones = Child(pivot.transform, "RimColliders");
        interactable.colliders.Clear();
        // Overlapping capsule segments form a real annular grab region.
        // No collider covers the centre or spokes.
        const int colliderCount = 32;
        for (int i = 0; i < colliderCount; i++)
        {
            float a = i * Mathf.PI * 2f / colliderCount;
            float b = (i + 1) * Mathf.PI * 2f / colliderCount;
            Vector3 p = radius * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 q = radius * new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
            GameObject segment = Child(zones.transform, "RimCollider_" + i.ToString("00"));
            segment.transform.localPosition = (p + q) * 0.5f;
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (q - p).normalized);
            CapsuleCollider collider = segment.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = tube;
            collider.height = (q - p).magnitude + tube * 2f;
            // XRI Direct Interactor overlap queries ignore triggers by default.
            collider.isTrigger = false;
            interactable.colliders.Add(collider);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI * 2f / 6f;
            Vector3 direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Cylinder(pivot.transform, "Spoke_" + (i + 1), direction * (radius * 0.5f),
                Quaternion.FromToRotation(Vector3.up, direction), new Vector3(0.022f, radius * 0.5f, 0.022f), red);
        }
        Cylinder(pivot.transform, "Hub", Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.075f, 0.025f, 0.075f), red);
        Cylinder(pivot.transform, "CentreBolt", new Vector3(0f, 0f, -0.032f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.032f, 0.01f, 0.032f), steel);
        Cylinder(root.transform, "FixedStem", new Vector3(0f, 0f, 0.075f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.035f, 0.06f, 0.035f), steel);
        AssetDatabase.SaveAssets();
        Undo.RegisterCreatedObjectUndo(root, "Create Large VR Valve");
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("CompactValve created. Save the scene, grab the rim with Grip, and move your hand around the wheel. Centre/spokes cannot be grabbed.", root);
    }

    static GameObject Child(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    static Material MakeMaterial(Shader shader, Color color, string folder, string name)
    {
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.3f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.4f);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat"));
        return material;
    }

    static void Cylinder(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = position;
        cylinder.transform.localRotation = rotation;
        cylinder.transform.localScale = scale;
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Mesh Torus(float radius, float tube, int rings, int sides)
    {
        Vector3[] vertices = new Vector3[(rings + 1) * (sides + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rings * sides * 6];
        for (int i = 0; i <= rings; i++)
        {
            float a = i * Mathf.PI * 2f / rings;
            Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= sides; j++)
            {
                float b = j * Mathf.PI * 2f / sides;
                int index = i * (sides + 1) + j;
                normals[index] = radial * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                vertices[index] = radial * radius + normals[index] * tube;
                uv[index] = new Vector2((float)i / rings, (float)j / sides);
            }
        }
        int t = 0;
        for (int i = 0; i < rings; i++) for (int j = 0; j < sides; j++)
        {
            int a = i * (sides + 1) + j;
            int b = a + sides + 1;
            triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
            triangles[t++] = b; triangles[t++] = b + 1; triangles[t++] = a + 1;
        }
        Mesh mesh = new Mesh { name = "CompactValveRim", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
}
#endif

