using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta a troca VR/MR na cena aberta (B2_TestRig): passthrough no projeto e no rig, o
/// <see cref="ExperienceModeController"/>, um chão invisível com colisor para o MR e o botão de poke no
/// pulso esquerdo (<see cref="WristModeToggle"/>). Põe a cena do ambiente no Build Settings.
///
/// Pode rodar de novo: refaz o botão do pulso e o chão do MR, e mantém o que já estiver nas listas de
/// cada modo do controlador.
/// </summary>
internal static class ExperienceModeSetup
{
    private const string EnvironmentScenePath = "Assets/_Project/Scenes/BackgroundIndoor.unity";
    private const string ButtonMaterialPath = "Assets/_Project/Arts/Materials/WristToggle.mat";

    // Botão do pulso: 7 × 4 cm, 6 mm de espessura, com as duas metades "VR" e "MR" embaixo do título.
    private static readonly Vector3 ButtonSize = new Vector3(0.07f, 0.04f, 0.006f);
    private const float SegmentWidth = 0.03f;
    private const float SegmentHeight = 0.02f;
    private const float SegmentGap = 0.001f;
    private static readonly Vector3 TableButtonSize = new Vector3(0.07f, 0.016f, 0.004f);

    // Mesa da cena de teste (colisor, sem render) que a calibração do MR alinha com a mesa real.
    private const string TableName = "ENV_Metal_Desk_01a";
    private const string GhostMaterialPath = "Assets/_Project/Arts/Materials/TableGhost.mat";

    [MenuItem("Tools/Modos/Montar troca VR-MR na cena aberta")]
    private static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("Não achei o OVRCameraRig na cena aberta.");
            return;
        }

        AddEnvironmentToBuild();
        EnablePassthrough(rig);

        // GetComponent no editor devolve um nulo "falso", então nada de ?? com componentes.
        var passthrough = rig.GetComponent<OVRPassthroughLayer>();
        if (passthrough == null)
        {
            passthrough = Undo.AddComponent<OVRPassthroughLayer>(rig.gameObject);
        }

        passthrough.enabled = false;

        GameObject floor = BuildMrFloor(scene);
        ExperienceModeController modes = BuildController(scene, passthrough, floor);
        TableCalibration calibration = BuildTableCalibration(scene, rig, modes, floor);
        BuildWristToggle(scene, rig, modes, calibration);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Troca VR/MR montada: ExperienceMode, MR_Floor, TableCalibration e WristModeToggle. Ambiente no Build Settings.");
    }

    private static void AddEnvironmentToBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(s => s.path != EnvironmentScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(EnvironmentScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void EnablePassthrough(OVRCameraRig rig)
    {
        OVRProjectConfig config = OVRProjectConfig.CachedProjectConfig;
        if (config.insightPassthroughSupport == OVRProjectConfig.FeatureSupport.None)
        {
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
        }

        // O passthrough fica ligado no OVRManager; o que mostra ou esconde é a camada no rig.
        var manager = rig.GetComponent<OVRManager>();
        if (manager != null)
        {
            Undo.RecordObject(manager, "Passthrough");
            manager.isInsightPassthroughEnabled = true;
        }
    }

    private static GameObject BuildMrFloor(Scene scene)
    {
        GameObject floor = Find(scene, "MR_Floor") ?? new GameObject("MR_Floor");
        Undo.RegisterCreatedObjectUndo(floor, "MR_Floor");
        floor.transform.SetPositionAndRotation(new Vector3(0f, -0.05f, 0f), Quaternion.identity);
        var box = floor.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = floor.AddComponent<BoxCollider>();
        }

        box.size = new Vector3(20f, 0.1f, 20f);
        floor.SetActive(false);
        return floor;
    }

    private static ExperienceModeController BuildController(Scene scene, OVRPassthroughLayer passthrough, GameObject floor)
    {
        GameObject go = Find(scene, "ExperienceMode") ?? new GameObject("ExperienceMode");
        Undo.RegisterCreatedObjectUndo(go, "ExperienceMode");
        var modes = go.GetComponent<ExperienceModeController>();
        if (modes == null)
        {
            modes = go.AddComponent<ExperienceModeController>();
        }


        var data = new SerializedObject(modes);
        data.FindProperty("_passthrough").objectReferenceValue = passthrough;

        // Só no MR: o chão invisível e a luz da cena de teste (no VR a luz vem do ambiente).
        var mrOnly = new List<Object>();
        SerializedProperty list = data.FindProperty("_mrOnly");
        for (int i = 0; i < list.arraySize; i++)
        {
            Object item = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (item != null)
            {
                mrOnly.Add(item);
            }
        }

        foreach (GameObject item in new[] { floor, Find(scene, "Directional Light") })
        {
            if (item != null && !mrOnly.Contains(item))
            {
                mrOnly.Add(item);
            }
        }

        list.arraySize = mrOnly.Count;
        for (int i = 0; i < mrOnly.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = mrOnly[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return modes;
    }

    private static TableCalibration BuildTableCalibration(Scene scene, OVRCameraRig rig, ExperienceModeController modes,
        GameObject floor)
    {
        GameObject desk = Find(scene, TableName);
        if (desk == null)
        {
            Debug.LogWarning($"Não achei a mesa '{TableName}': a calibração da mesa do MR não foi montada.");
            return null;
        }

        foreach (string name in new[] { "TableCalibration", "TableReference" })
        {
            GameObject old = Find(scene, name);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }
        }

        // Ponto de referência: meio da borda de cima do lado do rig, com o +Z para o outro lado da mesa.
        var box = desk.GetComponent<BoxCollider>();
        Vector3 toRig = desk.transform.InverseTransformPoint(rig.transform.position) - box.center;
        Vector3 nearSide = Mathf.Abs(toRig.x) * box.size.z > Mathf.Abs(toRig.z) * box.size.x
            ? new Vector3(Mathf.Sign(toRig.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(toRig.z));
        Vector3 localPoint = box.center + Vector3.up * (box.size.y * 0.5f) + Vector3.Scale(nearSide, box.size * 0.5f);

        var reference = new GameObject("TableReference");
        Undo.RegisterCreatedObjectUndo(reference, "TableReference");
        reference.transform.SetParent(desk.transform, false);
        reference.transform.localPosition = localPoint;
        reference.transform.localRotation = Quaternion.LookRotation(-nearSide, Vector3.up);

        var root = new GameObject("TableCalibration");
        Undo.RegisterCreatedObjectUndo(root, "TableCalibration");
        var calibration = root.AddComponent<TableCalibration>();

        // Fantasma: a mesma malha, transparente, com o mesmo ponto de referência num filho.
        var ghost = new GameObject("TableGhost");
        ghost.transform.SetParent(root.transform, false);
        ghost.transform.SetPositionAndRotation(desk.transform.position, desk.transform.rotation);
        ghost.AddComponent<MeshFilter>().sharedMesh = desk.GetComponent<MeshFilter>().sharedMesh;
        var ghostRenderer = ghost.AddComponent<MeshRenderer>();
        ghostRenderer.sharedMaterial = GhostMaterial();
        ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var ghostReference = new GameObject("Reference");
        ghostReference.transform.SetParent(ghost.transform, false);
        ghostReference.transform.localPosition = localPoint;
        ghostReference.transform.localRotation = reference.transform.localRotation;

        // Instrução acima da borda, virada para o jogador.
        TextMesh label = Text(ghostReference.transform, "Label", "", new Vector3(0f, 0.3f, 0f), 0.006f, Color.white);
        label.transform.localRotation = Quaternion.identity;

        Transform right = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ComprehensiveInteractorsRight");
        Transform left = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ComprehensiveInteractorsLeft");
        var data = new SerializedObject(calibration);
        data.FindProperty("_modes").objectReferenceValue = modes;
        data.FindProperty("_rig").objectReferenceValue = rig.transform;
        data.FindProperty("_tableReference").objectReferenceValue = reference.transform;
        data.FindProperty("_ghost").objectReferenceValue = ghost.transform;
        data.FindProperty("_ghostReference").objectReferenceValue = ghostReference.transform;
        data.FindProperty("_label").objectReferenceValue = label;
        data.FindProperty("_mrFloor").objectReferenceValue = floor.transform;
        data.FindProperty("_hand").objectReferenceValue = right != null ? right.GetComponent<Hand>() : null;
        data.FindProperty("_controller").objectReferenceValue = right != null ? right.GetComponent<Controller>() : null;
        data.FindProperty("_confirmHand").objectReferenceValue = left != null ? left.GetComponent<Hand>() : null;
        data.FindProperty("_confirmController").objectReferenceValue = left != null ? left.GetComponent<Controller>() : null;
        data.ApplyModifiedPropertiesWithoutUndo();
        return calibration;
    }

    /// <summary>Botão de poke: superfície no plano z = 0, com a frente para −Z (padrão da PlaneSurface).</summary>
    private static PokeInteractable PokeButton(Transform parent, string name, Vector3 position, Vector2 size)
    {
        var button = new GameObject(name);
        button.transform.SetParent(parent, false);
        button.transform.localPosition = position;
        var plane = button.AddComponent<PlaneSurface>();
        var clipper = button.AddComponent<BoundsClipper>();
        var clipped = button.AddComponent<ClippedPlaneSurface>();
        var poke = button.AddComponent<PokeInteractable>();

        var clipperData = new SerializedObject(clipper);
        clipperData.FindProperty("_position").vector3Value = Vector3.zero;
        clipperData.FindProperty("_size").vector3Value = new Vector3(size.x, size.y, 0.02f);
        clipperData.ApplyModifiedPropertiesWithoutUndo();

        var clippedData = new SerializedObject(clipped);
        clippedData.FindProperty("_planeSurface").objectReferenceValue = plane;
        SerializedProperty clippers = clippedData.FindProperty("_clippers");
        clippers.arraySize = 1;
        clippers.GetArrayElementAtIndex(0).objectReferenceValue = clipper;
        clippedData.ApplyModifiedPropertiesWithoutUndo();

        var pokeData = new SerializedObject(poke);
        pokeData.FindProperty("_surfacePatch").objectReferenceValue = clipped;
        pokeData.ApplyModifiedPropertiesWithoutUndo();
        return poke;
    }

    private static void BuildWristToggle(Scene scene, OVRCameraRig rig, ExperienceModeController modes,
        TableCalibration calibration)
    {
        GameObject old = Find(scene, "WristModeToggle");
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        var root = new GameObject("WristModeToggle");
        Undo.RegisterCreatedObjectUndo(root, "WristModeToggle");
        var toggle = root.AddComponent<WristModeToggle>();

        var content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);

        PokeInteractable poke = PokeButton(content.transform, "Button", Vector3.zero, ButtonSize);

        // Botão MESA, embaixo do seletor (só no MR).
        var tableButton = new GameObject("TableButton");
        tableButton.transform.SetParent(content.transform, false);
        float tableY = -(ButtonSize.y + TableButtonSize.y) * 0.5f - 0.004f;
        PokeInteractable tablePoke = PokeButton(tableButton.transform, "Button", new Vector3(0f, tableY, 0f), TableButtonSize);
        Renderer tableFrame = Plate(tableButton.transform, "Frame", new Vector3(0f, tableY, TableButtonSize.z * 0.5f), TableButtonSize);
        Text(tableButton.transform, "Label", "AJUSTAR MESA", new Vector3(0f, tableY, -0.0005f), 0.0008f, Color.white);

        // Visual: seletor "VR | MR". A moldura fica atrás do plano do poke (face da frente em z = 0), as
        // duas metades saem 2 mm para a frente e os textos ficam logo em cima delas.
        Renderer frame = Plate(content.transform, "Frame", new Vector3(0f, 0f, ButtonSize.z * 0.5f), ButtonSize);
        Text(content.transform, "Title", "MODO", new Vector3(0f, 0.0125f, -0.0005f), 0.0007f, new Color(0.8f, 0.82f, 0.85f));

        var segmentSize = new Vector3(SegmentWidth, SegmentHeight, 0.002f);
        Renderer vr = Plate(content.transform, "VR", new Vector3(-SegmentWidth * 0.5f - SegmentGap, -0.004f, -0.001f), segmentSize);
        Renderer mr = Plate(content.transform, "MR", new Vector3(SegmentWidth * 0.5f + SegmentGap, -0.004f, -0.001f), segmentSize);
        Text(vr.transform.parent, "VR Label", "VR", vr.transform.localPosition + new Vector3(0f, 0f, -0.0015f), 0.0011f, Color.white);
        Text(mr.transform.parent, "MR Label", "MR", mr.transform.localPosition + new Vector3(0f, 0f, -0.0015f), 0.0011f, Color.white);

        // Mão e controle esquerdos do rig do Interaction SDK.
        Transform left = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ComprehensiveInteractorsLeft");
        var data = new SerializedObject(toggle);
        data.FindProperty("_modes").objectReferenceValue = modes;
        data.FindProperty("_hand").objectReferenceValue = left != null ? left.GetComponent<Hand>() : null;
        data.FindProperty("_controller").objectReferenceValue = left != null ? left.GetComponent<Controller>() : null;
        data.FindProperty("_poke").objectReferenceValue = poke;
        data.FindProperty("_content").objectReferenceValue = content;
        data.FindProperty("_frame").objectReferenceValue = frame;
        data.FindProperty("_vrSegment").objectReferenceValue = vr;
        data.FindProperty("_mrSegment").objectReferenceValue = mr;
        data.FindProperty("_calibration").objectReferenceValue = calibration;
        data.FindProperty("_tablePoke").objectReferenceValue = tablePoke;
        data.FindProperty("_tableButton").objectReferenceValue = tableButton;
        data.FindProperty("_tableFrame").objectReferenceValue = tableFrame;
        data.ApplyModifiedPropertiesWithoutUndo();

        if (left == null)
        {
            Debug.LogWarning("Não achei ComprehensiveInteractorsLeft: ligue a mão e o controle no WristModeToggle à mão.");
        }
    }

    private static Renderer Plate(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = name;
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.transform.SetParent(parent, false);
        plate.transform.localPosition = position;
        plate.transform.localScale = size;
        var renderer = plate.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = ButtonMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static TextMesh Text(Transform parent, string name, string text, Vector3 position, float size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var label = go.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 48;
        label.characterSize = size;
        label.color = color;
        label.text = text;

        // Mesma fonte dos outros textos da cena, se houver.
        TextMesh other = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t != label && t.font != null && t.transform.root.name != "WristModeToggle");
        if (other != null)
        {
            label.font = other.font;
            go.GetComponent<MeshRenderer>().sharedMaterial = other.GetComponent<MeshRenderer>().sharedMaterial;
        }
        else
        {
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }

        return label;
    }

    private static Material GhostMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, GhostMaterialPath);
        }

        material.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1f, 0.35f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        BaseShaderGUI.SetMaterialKeywords(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material ButtonMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ButtonMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, ButtonMaterialPath);
        }

        return material;
    }

    private static GameObject Find(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject)
            .FirstOrDefault(go => go.name == name);
    }
}
