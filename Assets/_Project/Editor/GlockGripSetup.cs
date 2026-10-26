using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta a pegada da Glock (tarefa C1) no prefab: Rigidbody e colliders físicos da raiz, os
/// Grabbables (arma, ferrolho e alavanca) e as zonas de pegada com seus volumes e regras.
///
/// Pode rodar de novo: refaz volumes, prioridades e regras das zonas, mas mantém os filhos que
/// não foram criados aqui (por exemplo, as poses de snap). Ajustes feitos à mão nos volumes ou
/// nas regras se perdem ao rodar de novo.
/// </summary>
internal static class GlockGripSetup
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Glock.prefab";

    // Medidas no espaço local da raiz da Glock (metros, cano em +Z).
    private const float GripAngle = 20f;

    // O carregador é inclinado como o cabo: 19° da vertical, com o topo para a frente.
    private const float MagazineAngle = 19f;

    private struct Box
    {
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 Euler;

        public Box(Vector3 center, Vector3 size, Vector3 euler = default)
        {
            Center = center;
            Size = size;
            Euler = euler;
        }
    }

    /// <summary>Pose do pulso no espaço local da raiz da Glock.</summary>
    private struct WristPose
    {
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;

        public WristPose(Vector3 position, Vector3 forward, Vector3 up)
        {
            Position = position;
            Forward = forward;
            Up = up;
        }

        public static WristPose FromTransform(Transform root, Transform wrist)
        {
            return new WristPose(root.InverseTransformPoint(wrist.position),
                root.InverseTransformDirection(wrist.forward), root.InverseTransformDirection(wrist.up));
        }

        /// <summary>A mesma pose para a outra mão, espelhada no plano YZ da arma.</summary>
        public WristPose Mirrored()
        {
            Vector3 flip = new Vector3(-1f, 1f, 1f);
            return new WristPose(Vector3.Scale(Position, flip), Vector3.Scale(Forward, flip), Vector3.Scale(Up, flip));
        }

        public void ApplyTo(Transform root, Transform wrist)
        {
            wrist.SetPositionAndRotation(root.TransformPoint(Position),
                root.rotation * Quaternion.LookRotation(Forward, Up));
        }
    }

    [MenuItem("Tools/Glock/Montar zonas de pegada (C1)")]
    private static void Build()
    {
        EditPrefab(BuildGrip);
        Debug.Log("[Glock] Zonas de pegada montadas em " + PrefabPath);
    }

    /// <summary>
    /// Copia a pose de snap da mão direita para a esquerda em cada zona com snap, espelhando o
    /// pulso no plano YZ da arma e as juntas dos dedos. Para ajustar só a direita no editor.
    /// </summary>
    [MenuItem("Tools/Glock/Espelhar poses de snap (direita para esquerda)")]
    private static void MirrorPoses()
    {
        int count = 0;
        EditPrefab(root =>
        {
            foreach (GlockGripZone zone in root.GetComponentsInChildren<GlockGripZone>(true))
            {
                Transform right = zone.transform.Find("HandGrab_Right/Pose");
                Transform left = zone.transform.Find("HandGrab_Left/Pose");
                if (right == null || left == null)
                {
                    continue;
                }

                WristPose.FromTransform(root, right).Mirrored().ApplyTo(root, left);
                left.localScale = right.localScale;

                HandPose source = right.GetComponent<HandGrabPose>().HandPose;
                HandGrabPose target = left.GetComponent<HandGrabPose>();
                if (source != null)
                {
                    var mirrored = new HandPose(source) { Handedness = Handedness.Left };
                    for (int i = 0; i < mirrored.JointRotations.Length; i++)
                    {
                        mirrored.JointRotations[i] = HandMirroring.Mirror(mirrored.JointRotations[i]);
                    }

                    target.InjectOptionalHandPose(mirrored);
                }

                EditorUtility.SetDirty(target);
                count++;
            }
        });

        Debug.Log("[Glock] Poses espelhadas para a mão esquerda em " + count + " zona(s).");
    }

    /// <summary>
    /// Edita o prefab da Glock. Se ele estiver aberto no Prefab Mode, edita lá (e fica para
    /// salvar); senão, abre o asset, edita e salva.
    /// </summary>
    private static void EditPrefab(System.Action<Transform> edit)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            edit(stage.prefabContentsRoot.transform);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            edit(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildGrip(Transform root)
    {
        Transform slide = root.Find("SlideGroup");
        Transform lever = root.Find("TakedownLeverPivot");

        // Corpo físico: cai na mesa quando solto, fica cinemático enquanto seguro.
        Rigidbody body = GetOrAdd<Rigidbody>(root.gameObject);
        body.mass = 0.7f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Transform physics = Child(root, "PhysicsColliders");
        SetBoxes(physics, false,
            new Box(new Vector3(0f, 0.05f, -0.0656f), new Vector3(0.03f, 0.1f, 0.052f), new Vector3(GripAngle, 0f, 0f)),
            new Box(new Vector3(0f, 0.08f, -0.005f), new Vector3(0.029f, 0.04f, 0.07f)),
            new Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.029f, 0.014f, 0.19f)));
        // O collider do ferrolho anda com ele (sem Rigidbody próprio, faz parte do corpo da arma).
        SetBoxes(Child(slide, "SlideCollider"), false,
            new Box(slide.InverseTransformPoint(root.TransformPoint(new Vector3(0f, 0.121f, 0.0015f))),
                new Vector3(0.0246f, 0.038f, 0.187f)));

        Grabbable gun = SetupGrabbable(root, body, GetOrAdd<GrabFreeTransformer>(root.gameObject));
        SerializedObject gunData = new SerializedObject(gun);
        gunData.FindProperty("_kinematicWhileSelected").boolValue = true;
        gunData.FindProperty("_throwWhenUnselected").boolValue = true;
        gunData.ApplyModifiedPropertiesWithoutUndo();

        // O GlockSlide é o transformer do ferrolho: trilho na arma e pegada livre depois de desmontado.
        var oldSlideTranslate = slide.GetComponent<OneGrabTranslateTransformer>();
        if (oldSlideTranslate != null)
        {
            Object.DestroyImmediate(oldSlideTranslate);
        }

        Grabbable slideGrab = SetupGrabbable(slide, null, root.GetComponent<GlockSlide>());

        // Provisório até a D-fase: a alavanca aceita a pegada mas não se mexe.
        var leverTranslate = GetOrAdd<OneGrabTranslateTransformer>(lever.gameObject);
        Vector3 leverRest = lever.localPosition;
        LockTranslation(leverTranslate, leverRest, new Vector2(leverRest.z, leverRest.z));
        Grabbable leverGrab = SetupGrabbable(lever, null, leverTranslate);

        GlockGripController controller = GetOrAdd<GlockGripController>(root.gameObject);
        Transform zones = Child(root, "GripZones");

        // Poses de pulso da mão direita (a esquerda é espelhada em X), convenção OpenXR:
        // +Z do pulso aponta para os dedos e +Y para o dorso da mão. São aproximadas; ajuste no
        // editor com a mão fantasma (o builder não mexe numa pose que já existe).
        var handleWrist = new WristPose(new Vector3(0.03f, 0.096f, -0.13f),
            new Vector3(0f, -0.34f, 0.94f), Vector3.right);
        // Desmontagem: palma no lado do ferrolho, dedos para cima abraçando o ferrolho e polegar
        // indo para trás, até o beavertail.
        var takedownWrist = new WristPose(new Vector3(0.032f, 0.063f, -0.067f),
            new Vector3(0f, 1f, 0.15f), Vector3.right);

        var all = new List<GlockGripZone>
        {
            Zone(root, zones, "Zone_Handle", controller, gun, GlockGripMode.Handle, 20,
                GlockGripZone.OtherHandRule.Any, handleWrist,
                new Box(new Vector3(0f, 0.0534f, -0.075f), new Vector3(0.09f, 0.12f, 0.092f), new Vector3(GripAngle, 0f, 0f))),

            Zone(root, zones, "Zone_Takedown", controller, gun, GlockGripMode.Takedown, 40,
                GlockGripZone.OtherHandRule.MustNotHoldGun, takedownWrist,
                new Box(new Vector3(0f, 0.125f, -0.06f), new Vector3(0.1f, 0.08f, 0.11f))),

            Zone(root, zones, "Zone_SlideFree", controller, gun, GlockGripMode.SlideFree, 30,
                GlockGripZone.OtherHandRule.MustNotHoldGun, null,
                new Box(new Vector3(0f, 0.1325f, 0f), new Vector3(0.09f, 0.085f, 0.21f))),

            Zone(root, slide, "Zone_SlideDrag", controller, slideGrab, GlockGripMode.SlideDrag, 30,
                GlockGripZone.OtherHandRule.MustHoldGun, null,
                new Box(slide.InverseTransformPoint(root.TransformPoint(new Vector3(0f, 0.1325f, 0f))),
                    new Vector3(0.09f, 0.085f, 0.21f))),

            Zone(root, zones, "Zone_Lever", controller, leverGrab, GlockGripMode.Lever, 50,
                GlockGripZone.OtherHandRule.MustHoldGun, null,
                new Box(new Vector3(0f, 0.07f, 0.0175f), new Vector3(0.09f, 0.08f, 0.085f))),

            Zone(root, zones, "Zone_Free", controller, gun, GlockGripMode.Free, 0,
                GlockGripZone.OtherHandRule.Any, null,
                new Box(new Vector3(0f, 0.075f, -0.005f), new Vector3(0.1f, 0.19f, 0.25f))),
        };

        all.Add(BuildMagazine(root, controller));

        // Pegada de desmontagem: dedos para cima, abraçando o ferrolho. Na empunhadura eles apontam
        // para a frente, então o ângulo separa as duas mesmo com os volumes se tocando.
        all[1].InjectOptionalAngle(GlockGripZone.HandAxis.Fingers, Vector3.up, 50f);

        SerializedObject controllerData = new SerializedObject(controller);
        SerializedProperty zoneList = controllerData.FindProperty("_zones");
        zoneList.arraySize = all.Count;
        for (int i = 0; i < all.Count; i++)
        {
            zoneList.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        }

        controllerData.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Carregador removível: eixo do poço, corpo com física, pegada própria (o
    /// <see cref="GlockMagazine"/> é o transformer) e uma zona de pegada com prioridade alta.
    /// </summary>
    private static GlockGripZone BuildMagazine(Transform root, GlockGripController controller)
    {
        Transform axis = Child(root, "MagwellAxis");
        Transform magazine = axis.Find("Magazine");
        if (magazine == null)
        {
            // O eixo nasce na posição do carregador inserido, inclinado como ele.
            magazine = root.Find("Magazine");
            axis.SetPositionAndRotation(magazine.position, root.rotation * Quaternion.Euler(MagazineAngle, 0f, 0f));
            magazine.SetParent(axis, true);
        }

        Rigidbody body = GetOrAdd<Rigidbody>(magazine.gameObject);
        body.mass = 0.08f;
        body.isKinematic = true;
        // Sem interpolação dentro da arma; o GlockMagazine liga quando ele fica solto.
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Corpo do carregador alinhado com o eixo do poço (medidas no espaço da raiz da arma).
        Transform colliderOwner = Child(magazine, "MagazineCollider");
        SetBoxes(colliderOwner, false,
            new Box(colliderOwner.InverseTransformPoint(root.TransformPoint(new Vector3(0f, 0.0595f, -0.059f))),
                new Vector3(0.022f, 0.126f, 0.034f), new Vector3(MagazineAngle, 0f, 0f)));

        GlockMagazine magazineLogic = GetOrAdd<GlockMagazine>(magazine.gameObject);
        Grabbable grabbable = SetupGrabbable(magazine, body, magazineLogic);
        SerializedObject grabData = new SerializedObject(grabbable);
        grabData.FindProperty("_kinematicWhileSelected").boolValue = true;
        grabData.FindProperty("_throwWhenUnselected").boolValue = true;
        grabData.ApplyModifiedPropertiesWithoutUndo();

        GlockGripZone zone = GetOrAdd<GlockGripZone>(magazine.gameObject);
        HandGrabInteractable interactable = Grab(magazine, body, grabbable, zone, HandAlignType.None);
        zone.InjectAllGlockGripZone(controller, new[] { interactable }, GlockGripMode.Magazine, 60,
            GlockGripZone.OtherHandRule.Any);
        EditorUtility.SetDirty(zone);

        var gunColliders = new List<Collider>(root.Find("PhysicsColliders").GetComponentsInChildren<Collider>(true));
        gunColliders.AddRange(root.Find("SlideGroup/SlideCollider").GetComponentsInChildren<Collider>(true));

        SerializedObject data = new SerializedObject(magazineLogic);
        data.FindProperty("_grip").objectReferenceValue = controller;
        data.FindProperty("_slideStop").objectReferenceValue = root.GetComponent<GlockSlideStop>();
        data.FindProperty("_wellAxis").objectReferenceValue = axis;
        data.FindProperty("_rigidbody").objectReferenceValue = body;
        data.FindProperty("_interactable").objectReferenceValue = interactable;
        data.FindProperty("_zone").objectReferenceValue = zone;
        data.FindProperty("_releaseButton").objectReferenceValue = root.Find("MagReleasePivot");
        SerializedProperty colliders = data.FindProperty("_gunColliders");
        colliders.arraySize = gunColliders.Count;
        for (int i = 0; i < gunColliders.Count; i++)
        {
            colliders.GetArrayElementAtIndex(i).objectReferenceValue = gunColliders[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return zone;
    }

    private static Grabbable SetupGrabbable(Transform target, Rigidbody body, ITransformer transformer)
    {
        Grabbable grabbable = GetOrAdd<Grabbable>(target.gameObject);
        grabbable.InjectOptionalTargetTransform(target);
        grabbable.InjectOptionalRigidbody(body);
        grabbable.InjectOptionalOneGrabTransformer(transformer);
        grabbable.MaxGrabPoints = 1;
        EditorUtility.SetDirty(grabbable);
        return grabbable;
    }

    private static void LockTranslation(OneGrabTranslateTransformer transformer, Vector3 rest, Vector2 zRange)
    {
        SerializedObject data = new SerializedObject(transformer);
        data.FindProperty("_constraints.ConstraintsAreRelative").boolValue = false;
        SetConstraint(data, "MinX", rest.x);
        SetConstraint(data, "MaxX", rest.x);
        SetConstraint(data, "MinY", rest.y);
        SetConstraint(data, "MaxY", rest.y);
        SetConstraint(data, "MinZ", zRange.x);
        SetConstraint(data, "MaxZ", zRange.y);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetConstraint(SerializedObject data, string name, float value)
    {
        data.FindProperty("_constraints." + name + ".Constrain").boolValue = true;
        data.FindProperty("_constraints." + name + ".Value").floatValue = value;
    }

    private static GlockGripZone Zone(Transform root, Transform parent, string name,
        GlockGripController controller, Grabbable target, GlockGripMode mode, int priority,
        GlockGripZone.OtherHandRule otherHand, WristPose? snapWrist, params Box[] volumes)
    {
        Transform zone = Child(parent, name);
        SetBoxes(zone, true, volumes);

        // Rigidbody próprio para os HandGrabInteractables enxergarem só os volumes desta zona.
        Rigidbody body = GetOrAdd<Rigidbody>(zone.gameObject);
        body.isKinematic = true;
        body.useGravity = false;

        GlockGripZone gripZone = GetOrAdd<GlockGripZone>(zone.gameObject);
        HandGrabInteractable[] interactables;
        if (snapWrist.HasValue)
        {
            // Snap: um HandGrabInteractable por mão, cada um com a sua pose de pulso.
            HandGrabInteractable single = zone.GetComponent<HandGrabInteractable>();
            if (single != null)
            {
                Object.DestroyImmediate(single);
            }

            interactables = new[]
            {
                Grab(Child(zone, "HandGrab_Right"), body, target, gripZone, HandAlignType.AlignOnGrab),
                Grab(Child(zone, "HandGrab_Left"), body, target, gripZone, HandAlignType.AlignOnGrab),
            };
            SnapPose(root, interactables[0], Handedness.Right, snapWrist.Value);
            SnapPose(root, interactables[1], Handedness.Left, snapWrist.Value.Mirrored());
        }
        else
        {
            interactables = new[] { Grab(zone, body, target, gripZone, HandAlignType.None) };
        }

        gripZone.InjectAllGlockGripZone(controller, interactables, mode, priority, otherHand);
        EditorUtility.SetDirty(gripZone);
        return gripZone;
    }

    private static HandGrabInteractable Grab(Transform owner, Rigidbody body, Grabbable target,
        GlockGripZone filter, HandAlignType align)
    {
        HandGrabInteractable interactable = GetOrAdd<HandGrabInteractable>(owner.gameObject);
        interactable.InjectSupportedGrabTypes(GrabTypeFlags.Palm);
        interactable.InjectRigidbody(body);
        interactable.InjectOptionalPointableElement(target);
        interactable.InjectOptionalInteractorFilters(new List<IGameObjectFilter> { filter });

        // Pegada de palma: basta um entre médio, anelar e mínimo fechar (com a arma deitada na
        // mesa os dedos não dão a volta no cabo). Polegar e indicador são ignorados: o indicador
        // fica livre para o gatilho e o gatilho do controle nunca pega a arma.
        SerializedObject data = new SerializedObject(interactable);
        data.FindProperty("_handAligment").enumValueIndex = (int)align;
        data.FindProperty("_palmGrabRules._thumbRequirement").enumValueIndex = (int)FingerRequirement.Ignored;
        data.FindProperty("_palmGrabRules._indexRequirement").enumValueIndex = (int)FingerRequirement.Ignored;
        data.FindProperty("_palmGrabRules._middleRequirement").enumValueIndex = (int)FingerRequirement.Optional;
        data.FindProperty("_palmGrabRules._ringRequirement").enumValueIndex = (int)FingerRequirement.Optional;
        data.FindProperty("_palmGrabRules._pinkyRequirement").enumValueIndex = (int)FingerRequirement.Optional;
        data.FindProperty("_palmGrabRules._unselectMode").enumValueIndex = (int)FingerUnselectMode.AllReleased;
        data.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(interactable);
        return interactable;
    }

    /// <summary>
    /// Cria a pose de pulso de uma mão, só com o pulso: os dedos ficam livres (seguem a mão real
    /// ou o controle). Se a pose já existe, só reconecta, para não perder ajustes manuais.
    /// </summary>
    private static void SnapPose(Transform root, HandGrabInteractable interactable, Handedness handedness,
        WristPose wrist)
    {
        Transform owner = interactable.transform;
        bool created = owner.Find("Pose") == null;
        Transform pose = Child(owner, "Pose");
        if (created)
        {
            wrist.ApplyTo(root, pose);
        }

        HandGrabPose grabPose = GetOrAdd<HandGrabPose>(pose.gameObject);
        grabPose.InjectRelativeTo(interactable.RelativeTo);
        if (created)
        {
            var handPose = new HandPose { Handedness = handedness };
            IReadOnlyHandSkeletonJointList skeleton = handedness == Handedness.Left
                ? HandSkeleton.DefaultLeftSkeleton
                : HandSkeleton.DefaultRightSkeleton;
            for (int i = 0; i < FingersMetadata.HAND_JOINT_IDS.Length; i++)
            {
                handPose.JointRotations[i] = skeleton[(int)FingersMetadata.HAND_JOINT_IDS[i]].pose.rotation;
            }

            for (int i = 0; i < handPose.FingersFreedom.Length; i++)
            {
                handPose.FingersFreedom[i] = JointFreedom.Free;
            }

            grabPose.InjectOptionalHandPose(handPose);
        }

        interactable.InjectOptionalHandGrabPoses(new List<HandGrabPose> { grabPose });
        EditorUtility.SetDirty(grabPose);
        EditorUtility.SetDirty(interactable);
    }

    /// <summary>
    /// Troca os filhos "Volume_N" de <paramref name="owner"/> por novos BoxColliders.
    /// </summary>
    private static void SetBoxes(Transform owner, bool isTrigger, params Box[] boxes)
    {
        for (int i = owner.childCount - 1; i >= 0; i--)
        {
            Transform child = owner.GetChild(i);
            if (child.name.StartsWith("Volume_"))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            Transform volume = Child(owner, "Volume_" + i);
            volume.localPosition = boxes[i].Center;
            volume.localRotation = Quaternion.Euler(boxes[i].Euler);
            BoxCollider box = volume.gameObject.AddComponent<BoxCollider>();
            box.size = boxes[i].Size;
            box.isTrigger = isTrigger;
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }

        child.gameObject.layer = parent.gameObject.layer;
        return child;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
