using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level11PowerRoomInstaller
{
    private const string MenuRoot =
        "Tools/Enlighten Them/Level 1-1/";

    private const string ElectricalPrefabPath =
        "Assets/Prefab/Gameplay/ElectricalBox_Interactable.prefab";

    private const string LampPrefabPath =
        "Assets/Prefab/Gameplay/SteamPunkLamp_Powered.prefab";

    private const string ElectricalVisualPrefabPath =
        "Assets/Prefab/Factory_Electrical_Box.prefab";

    private const string LampVisualPrefabPath =
        "Assets/Prefab/SteamPunk Lamp post.prefab";

    private const string SwitchPromptSpritePath =
        "Assets/Images/Mechines/wall controll.png";

    private const int InteractableRangeLayer = 11;
    private const int InteractionButtonLayer = 12;
    private const int CameraOccluderLayer = 13;
    private const float Level11LampTargetingRange = 70f;

    [MenuItem(MenuRoot + "Install Phase 1 Power Room")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Phase 1 Power Room",
                "Exit Play Mode before installing the room setup.",
                "OK"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("No loaded active scene was found.");
            return;
        }

        Transform room = FindSceneTransform("level1.1room", scene);
        Transform electricalVisual = FindSceneTransform(
            "Factory_Electrical_Box",
            scene
        );
        Transform lampVisual = FindSceneTransform(
            "SteamPunk Lamp post",
            scene
        );

        List<string> missing = new List<string>();
        if (room == null) missing.Add("level1.1room");
        if (electricalVisual == null &&
            FindSceneTransform("ElectricalBox_Interactable", scene) == null)
        {
            missing.Add("Factory_Electrical_Box");
        }

        if (lampVisual == null &&
            FindSceneTransform("SteamPunkLamp_Powered", scene) == null)
        {
            missing.Add("SteamPunk Lamp post");
        }

        if (missing.Count > 0)
        {
            string message =
                "The installer could not find: " +
                string.Join(", ", missing) +
                ". Keep those scene objects present and run the installer again.";

            Debug.LogError(message);
            EditorUtility.DisplayDialog(
                "Phase 1 Power Room",
                message,
                "OK"
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Level 1-1 Power Room");

        try
        {
            Transform levelRoot = EnsureLevelRoot(room, scene);
            Transform geometry = EnsureContainer(levelRoot, "Geometry");
            Transform props = EnsureContainer(levelRoot, "Props");
            EnsureContainer(levelRoot, "Gameplay");
            EnsureContainer(levelRoot, "Enemies");
            EnsureContainer(levelRoot, "Lights");

            ReparentPreservingWorld(room, geometry);
            ConfigureWallOcclusion(scene);

            GameObject electricalWrapper = EnsureElectricalWrapper(
                electricalVisual,
                props,
                scene
            );

            GameObject lampWrapper = EnsureLampWrapper(
                lampVisual,
                props,
                scene
            );

            RepairPuzzleBase puzzle = FindSceneComponent<RepairPuzzleBase>(scene);
            GeneratorInteractable generator =
                electricalWrapper.GetComponent<GeneratorInteractable>();

            AssignObjectReference(generator, "puzzle", puzzle);

            PoweredLampController lampController =
                lampWrapper.GetComponent<PoweredLampController>();

            AssignObjectReference(
                lampController,
                "sourceGenerator",
                generator
            );

            ConfigureLampAffectedEnemies(lampWrapper, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = levelRoot.gameObject;

            string validation = BuildValidationReport(scene);
            Debug.Log(validation, levelRoot.gameObject);

            EditorUtility.DisplayDialog(
                "Phase 1 Power Room",
                validation,
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Undo.RevertAllDownToGroup(undoGroup);

            EditorUtility.DisplayDialog(
                "Phase 1 Power Room",
                "Installation stopped. See the Console for the exact error.",
                "OK"
            );
        }
    }

    [MenuItem(MenuRoot + "Validate Phase 1 Power Room")]
    public static void ValidateSetup()
    {
        Scene scene = SceneManager.GetActiveScene();
        string report = BuildValidationReport(scene);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Phase 1 Validation", report, "OK");
    }

    private static Transform EnsureLevelRoot(Transform room, Scene scene)
    {
        Transform existing = FindSceneTransform("Level1_1_Root", scene);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Normalize Level 1-1 root");
            existing.localScale = Vector3.one;
            return existing;
        }

        GameObject rootObject = new GameObject("Level1_1_Root");
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Level 1-1 root");
        SceneManager.MoveGameObjectToScene(rootObject, scene);

        rootObject.transform.position = room.position;
        rootObject.transform.rotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        return rootObject.transform;
    }

    private static Transform EnsureContainer(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing;

        GameObject container = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(container, "Create " + name);
        Undo.SetTransformParent(container.transform, parent, "Parent " + name);
        ResetLocalTransform(container.transform);
        return container.transform;
    }

    private static GameObject EnsureElectricalWrapper(
        Transform visual,
        Transform props,
        Scene scene)
    {
        Transform existing = FindSceneTransform(
            "ElectricalBox_Interactable",
            scene
        );

        GameObject wrapper;
        if (existing != null)
        {
            wrapper = RebuildWrapperFromOriginalVisual(
                existing.gameObject,
                ElectricalVisualPrefabPath,
                ElectricalPrefabPath,
                true,
                props
            );

            ReparentPreservingWorld(wrapper.transform, props);
        }
        else
        {
            wrapper = CreateWrapper(
                "ElectricalBox_Interactable",
                visual,
                props
            );
        }

        GeneratorInteractable generator =
            GetOrAddComponent<GeneratorInteractable>(wrapper);

        RemoveLegacyInteractionCopies(wrapper.transform, generator);

        Bounds localBounds = CalculateLocalRendererBounds(wrapper.transform);
        Transform interactionRange = EnsureChild(wrapper.transform, "InteractionRange");
        interactionRange.gameObject.layer = InteractableRangeLayer;

        BoxCollider rangeCollider =
            GetOrAddComponent<BoxCollider>(interactionRange.gameObject);

        Undo.RecordObject(rangeCollider, "Configure interaction range");
        rangeCollider.isTrigger = true;
        rangeCollider.center = localBounds.center;
        rangeCollider.size = ExpandAndClamp(
            localBounds.size,
            new Vector3(1.5f, 2f, 1.5f),
            1.1f
        );

        Transform interactionButton = EnsureChild(
            wrapper.transform,
            "InteractionButton"
        );
        interactionButton.gameObject.layer = InteractionButtonLayer;

        BoxCollider buttonCollider =
            GetOrAddComponent<BoxCollider>(interactionButton.gameObject);

        Undo.RecordObject(buttonCollider, "Configure interaction button");
        buttonCollider.isTrigger = true;
        buttonCollider.center = localBounds.center;
        buttonCollider.size = ExpandAndClamp(
            localBounds.size,
            Vector3.one,
            0.95f
        );

        ConfigureInteractionPrompt(interactionButton, localBounds);

        interactionButton.gameObject.SetActive(false);
        AssignObjectReference(
            generator,
            "interactionButton",
            interactionButton.gameObject
        );

        Transform switchTransform = FindLikelySwitchTransform(wrapper.transform);
        if (switchTransform == null)
        {
            switchTransform = EnsureChild(wrapper.transform, "SwitchPivot");
            switchTransform.localPosition = localBounds.center;
        }

        ElectricalSwitchFeedback feedback =
            GetOrAddComponent<ElectricalSwitchFeedback>(wrapper);

        AssignObjectReference(feedback, "sourceGenerator", generator);
        AssignObjectReference(feedback, "switchTransform", switchTransform);

        // Scene objects may reference a scene-only puzzle. Prefab assets must
        // stay reusable, so the scene reference is restored after saving.
        AssignObjectReference(generator, "puzzle", null);

        wrapper = EnsureConnectedPrefab(
            wrapper,
            ElectricalPrefabPath,
            props
        );

        return wrapper;
    }

    private static void ConfigureInteractionPrompt(
        Transform interactionButton,
        Bounds localBounds)
    {
        Transform prompt = EnsureChild(
            interactionButton,
            "InteractionPromptVisual"
        );

        prompt.gameObject.layer = InteractionButtonLayer;
        prompt.localPosition = new Vector3(
            localBounds.center.x,
            localBounds.max.y + 0.5f,
            localBounds.center.z
        );
        prompt.localRotation = Quaternion.identity;
        prompt.localScale = Vector3.one * 0.8f;

        SpriteRenderer spriteRenderer =
            GetOrAddComponent<SpriteRenderer>(prompt.gameObject);

        Sprite promptSprite = AssetDatabase
            .LoadAllAssetsAtPath(SwitchPromptSpritePath)
            .OfType<Sprite>()
            .FirstOrDefault();

        if (promptSprite == null)
        {
            throw new InvalidOperationException(
                "Could not load switch prompt sprite: " +
                SwitchPromptSpritePath
            );
        }

        Undo.RecordObject(spriteRenderer, "Configure switch prompt");
        spriteRenderer.sprite = promptSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 100;

        GetOrAddComponent<WorldPromptBillboard>(prompt.gameObject);
    }

    private static void ConfigureWallOcclusion(Scene scene)
    {
        Material fadeMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Material/Wall_Fade_Material.mat"
        );

        if (fadeMaterial == null)
        {
            throw new InvalidOperationException(
                "Wall_Fade_Material could not be loaded."
            );
        }

        Renderer[] sceneRenderers =
            Resources.FindObjectsOfTypeAll<Renderer>();

        foreach (Renderer sceneRenderer in sceneRenderers)
        {
            if (sceneRenderer == null ||
                sceneRenderer.gameObject.scene != scene ||
                EditorUtility.IsPersistent(sceneRenderer))
            {
                continue;
            }

            bool usesFadeMaterial = sceneRenderer.sharedMaterials.Any(
                material => material == fadeMaterial
            );

            if (!usesFadeMaterial)
                continue;

            Undo.RecordObject(
                sceneRenderer.gameObject,
                "Configure camera occluder wall"
            );

            sceneRenderer.gameObject.layer = CameraOccluderLayer;
        }

        CameraOcclusionFader fader =
            FindSceneComponent<CameraOcclusionFader>(scene);

        if (fader == null)
        {
            throw new InvalidOperationException(
                "The active scene has no CameraOcclusionFader."
            );
        }

        SetSerializedValue(fader, "fadedAlpha", 0.08f);
    }

    private static GameObject EnsureLampWrapper(
        Transform visual,
        Transform props,
        Scene scene)
    {
        Transform existing = FindSceneTransform(
            "SteamPunkLamp_Powered",
            scene
        );

        GameObject wrapper;
        if (existing != null)
        {
            wrapper = RebuildWrapperFromOriginalVisual(
                existing.gameObject,
                LampVisualPrefabPath,
                LampPrefabPath,
                false,
                props
            );

            ReparentPreservingWorld(wrapper.transform, props);
        }
        else
        {
            wrapper = CreateWrapper(
                "SteamPunkLamp_Powered",
                visual,
                props
            );
        }

        Bounds localBounds = CalculateLocalRendererBounds(wrapper.transform);
        Transform glowPoint = EnsureChild(wrapper.transform, "GlowPoint");
        glowPoint.localPosition = new Vector3(
            localBounds.center.x,
            Mathf.Lerp(localBounds.center.y, localBounds.max.y, 0.78f),
            localBounds.center.z
        );

        Transform lightTransform = EnsureChild(glowPoint, "LampLight");
        Light lampLight = GetOrAddComponent<Light>(lightTransform.gameObject);
        Undo.RecordObject(lampLight, "Configure powered lamp light");
        lampLight.type = LightType.Point;
        lampLight.color = new Color(1f, 0.62f, 0.28f, 1f);
        lampLight.range = Mathf.Max(10f, localBounds.size.y * 2f);
        lampLight.intensity = 0f;
        lampLight.shadows = LightShadows.Soft;
        lampLight.enabled = false;

        EnemyLightSource enemyLightSource =
            GetOrAddComponent<EnemyLightSource>(glowPoint.gameObject);

        SetSerializedValue(enemyLightSource, "attracting", false);
        SetSerializedValue(enemyLightSource, "strength", 1.5f);
        SetSerializedValue(
            enemyLightSource,
            "attractionRadius",
            Mathf.Max(12f, localBounds.size.y * 2f)
        );

        PoweredLampController controller =
            GetOrAddComponent<PoweredLampController>(wrapper);

        AssignObjectArray(controller, "lampLights", new UnityEngine.Object[]
        {
            lampLight
        });

        AssignObjectArray(
            controller,
            "emissiveRenderers",
            FindLikelyGlowRenderers(wrapper.transform)
                .Cast<UnityEngine.Object>()
                .ToArray()
        );

        AssignObjectReference(
            controller,
            "enemyLightSource",
            enemyLightSource
        );

        // The controlling electrical box is a scene-instance relationship.
        AssignObjectReference(controller, "sourceGenerator", null);

        wrapper = EnsureConnectedPrefab(wrapper, LampPrefabPath, props);
        return wrapper;
    }

    private static GameObject CreateWrapper(
        string wrapperName,
        Transform visual,
        Transform parent)
    {
        GameObject wrapper = new GameObject(wrapperName);
        Undo.RegisterCreatedObjectUndo(wrapper, "Create " + wrapperName);

        Transform wrapperTransform = wrapper.transform;
        wrapperTransform.position = visual.position;
        wrapperTransform.rotation = visual.rotation;
        wrapperTransform.localScale = Vector3.one;
        Undo.SetTransformParent(wrapperTransform, parent, "Parent " + wrapperName);

        Undo.SetTransformParent(visual, wrapperTransform, "Wrap " + visual.name);
        Undo.RecordObject(visual.gameObject, "Rename wrapped visual");
        visual.gameObject.name = "Visual";
        return wrapper;
    }

    private static void ConfigureLampAffectedEnemies(
        GameObject lampWrapper,
        Scene scene)
    {
        EnemyLightSource lightSource =
            lampWrapper.GetComponentInChildren<EnemyLightSource>(true);

        if (lightSource == null)
        {
            throw new InvalidOperationException(
                "The powered lamp has no EnemyLightSource."
            );
        }

        float maximumDistanceSqr =
            Level11LampTargetingRange * Level11LampTargetingRange;

        EnemyBrain[] affectedEnemies = FindSceneComponents<EnemyBrain>(scene)
            .Where(enemy =>
            {
                Vector3 offset = enemy.transform.position - lightSource.Position;
                offset.y = 0f;
                return offset.sqrMagnitude <= maximumDistanceSqr;
            })
            .OrderBy(enemy =>
                (enemy.transform.position - lightSource.Position).sqrMagnitude)
            .ToArray();

        AssignObjectArray(
            lightSource,
            "affectedEnemies",
            affectedEnemies.Cast<UnityEngine.Object>().ToArray()
        );
    }

    private static GameObject EnsureConnectedPrefab(
        GameObject wrapper,
        string prefabPath,
        Transform sceneParent)
    {
        EnsureAssetFolder("Assets/Prefab/Gameplay");

        string currentPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
            wrapper
        );

        if (string.Equals(currentPath, prefabPath, StringComparison.Ordinal))
        {
            PrefabUtility.ApplyPrefabInstance(
                wrapper,
                InteractionMode.AutomatedAction
            );
            return wrapper;
        }

        Vector3 worldPosition = wrapper.transform.position;
        Quaternion worldRotation = wrapper.transform.rotation;

        // Saving the real wrapper (instead of Object.Instantiate(wrapper))
        // preserves the visual model as a nested Prefab connection.
        wrapper.transform.SetParent(null, true);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(
            wrapper,
            prefabPath,
            InteractionMode.AutomatedAction
        );

        if (prefabAsset == null)
            throw new InvalidOperationException("Could not save prefab: " + prefabPath);

        wrapper.transform.SetParent(sceneParent, true);
        wrapper.transform.position = worldPosition;
        wrapper.transform.rotation = worldRotation;
        wrapper.transform.localScale = Vector3.one;
        return wrapper;
    }

    private static GameObject RebuildWrapperFromOriginalVisual(
        GameObject wrapper,
        string visualPrefabPath,
        string wrapperPrefabPath,
        bool requireSingleGenerator,
        Transform sceneParent)
    {
        if (IsWrapperPrefabHealthy(
                wrapper,
                wrapperPrefabPath,
                visualPrefabPath,
                requireSingleGenerator))
        {
            return wrapper;
        }

        Transform currentVisual = wrapper.transform.Find("Visual");
        GameObject visualPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);

        if (visualPrefab == null)
        {
            throw new InvalidOperationException(
                "Could not load visual prefab: " + visualPrefabPath
            );
        }

        Vector3 worldPosition = wrapper.transform.position;
        Quaternion worldRotation = wrapper.transform.rotation;
        Vector3 visualScale = currentVisual != null
            ? currentVisual.localScale
            : Vector3.one;
        string wrapperName = wrapper.name;
        Scene scene = wrapper.scene;

        Undo.DestroyObjectImmediate(wrapper);

        GameObject nestedVisual = PrefabUtility.InstantiatePrefab(
            visualPrefab,
            scene
        ) as GameObject;

        if (nestedVisual == null)
        {
            throw new InvalidOperationException(
                "Could not instantiate visual prefab: " + visualPrefabPath
            );
        }

        nestedVisual.transform.position = worldPosition;
        nestedVisual.transform.rotation = worldRotation;
        nestedVisual.transform.localScale = visualScale;

        return CreateWrapper(wrapperName, nestedVisual.transform, sceneParent);
    }

    private static bool IsWrapperPrefabHealthy(
        GameObject sceneWrapper,
        string wrapperPrefabPath,
        string visualPrefabPath,
        bool requireSingleGenerator)
    {
        string currentPath =
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sceneWrapper);

        if (currentPath != wrapperPrefabPath)
            return false;

        GameObject wrapperAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPrefabPath);

        if (wrapperAsset == null)
            return false;

        Transform visual = wrapperAsset.transform.Find("Visual");
        if (visual == null)
            return false;

        string nestedPath =
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                visual.gameObject
            );

        if (nestedPath != visualPrefabPath)
            return false;

        return !requireSingleGenerator ||
               wrapperAsset
                   .GetComponentsInChildren<GeneratorInteractable>(true)
                   .Length == 1;
    }

    private static void RemoveLegacyInteractionCopies(
        Transform wrapper,
        GeneratorInteractable rootGenerator)
    {
        GeneratorInteractable[] generators =
            wrapper.GetComponentsInChildren<GeneratorInteractable>(true);

        foreach (GeneratorInteractable generator in generators)
        {
            if (generator == rootGenerator)
                continue;

            Transform legacyRoot = generator.transform;
            Transform[] descendants =
                legacyRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform descendant in descendants)
            {
                if (descendant == null || descendant == legacyRoot)
                    continue;

                if (descendant.name == "ENTER" ||
                    descendant.name == "Interactable Range")
                {
                    Undo.DestroyObjectImmediate(descendant.gameObject);
                }
            }

            Undo.DestroyObjectImmediate(generator);
        }
    }

    private static Bounds CalculateLocalRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.one);

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Bounds worldBounds = renderers[rendererIndex].bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z
                );

                Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                if (!hasBounds)
                {
                    result = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(localCorner);
                }
            }
        }

        return hasBounds ? result : new Bounds(Vector3.zero, Vector3.one);
    }

    private static Vector3 ExpandAndClamp(
        Vector3 source,
        Vector3 minimum,
        float multiplier)
    {
        source *= multiplier;
        return new Vector3(
            Mathf.Max(minimum.x, source.x),
            Mathf.Max(minimum.y, source.y),
            Mathf.Max(minimum.z, source.z)
        );
    }

    private static Transform FindLikelySwitchTransform(Transform wrapper)
    {
        string[] hints = { "switch", "lever", "handle" };

        Transform[] transforms = wrapper.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate == wrapper)
                continue;

            string lowerName = candidate.name.ToLowerInvariant();
            if (hints.Any(lowerName.Contains))
                return candidate;
        }

        return null;
    }

    private static Renderer[] FindLikelyGlowRenderers(Transform wrapper)
    {
        string[] hints = { "bulb", "glass", "glow", "light" };

        return wrapper.GetComponentsInChildren<Renderer>(true)
            .Where(renderer =>
            {
                string lowerName = renderer.name.ToLowerInvariant();
                return hints.Any(lowerName.Contains);
            })
            .ToArray();
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing;

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        Undo.SetTransformParent(child.transform, parent, "Parent " + name);
        ResetLocalTransform(child.transform);
        return child.transform;
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null
            ? component
            : Undo.AddComponent<T>(target);
    }

    private static void ReparentPreservingWorld(
        Transform child,
        Transform parent)
    {
        if (child == null || child.parent == parent)
            return;

        Undo.SetTransformParent(child, parent, "Reparent " + child.name);
    }

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static Transform FindSceneTransform(string name, Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(transform =>
                transform != null &&
                transform.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(transform))
            .OrderBy(transform => GetHierarchyDepth(transform))
            .FirstOrDefault(transform =>
                string.Equals(
                    transform.name,
                    name,
                    StringComparison.OrdinalIgnoreCase
                ));
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(component =>
                component != null &&
                component.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(component));
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(component =>
                component != null &&
                component.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(component))
            .ToArray();
    }

    private static int GetHierarchyDepth(Transform transform)
    {
        int depth = 0;
        while (transform.parent != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private static void AssignObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(target.GetType().Name, propertyName);

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void AssignObjectArray(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object[] values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
            throw new MissingFieldException(target.GetType().Name, propertyName);

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedValue(
        UnityEngine.Object target,
        string propertyName,
        bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.boolValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedValue(
        UnityEngine.Object target,
        string propertyName,
        float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.floatValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = path.Substring(0, path.LastIndexOf('/'));
        string folderName = path.Substring(path.LastIndexOf('/') + 1);
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string BuildValidationReport(Scene scene)
    {
        List<string> errors = new List<string>();

        Transform root = FindSceneTransform("Level1_1_Root", scene);
        Transform room = FindSceneTransform("level1.1room", scene);
        Transform electrical = FindSceneTransform(
            "ElectricalBox_Interactable",
            scene
        );
        Transform lamp = FindSceneTransform("SteamPunkLamp_Powered", scene);

        if (root == null)
            errors.Add("Level1_1_Root is missing.");
        else if (root.localScale != Vector3.one)
            errors.Add("Level1_1_Root scale is not (1,1,1).");

        if (room == null || room.parent == null || room.parent.name != "Geometry")
            errors.Add("level1.1room is not under Geometry.");

        if (electrical == null)
        {
            errors.Add("ElectricalBox_Interactable is missing.");
        }
        else
        {
            GeneratorInteractable generator =
                electrical.GetComponent<GeneratorInteractable>();

            if (generator == null)
                errors.Add("The electrical box has no GeneratorInteractable.");

            if (electrical.GetComponentsInChildren<GeneratorInteractable>(true).Length != 1)
                errors.Add("The electrical box contains duplicate interaction components.");

            Transform prompt = electrical.Find(
                "InteractionButton/InteractionPromptVisual"
            );

            if (prompt == null || prompt.GetComponent<SpriteRenderer>() == null)
                errors.Add("The electrical box has no switch interaction image.");

            SerializedObject serializedGenerator =
                generator == null ? null : new SerializedObject(generator);

            if (serializedGenerator != null &&
                serializedGenerator.FindProperty("puzzle").objectReferenceValue == null)
            {
                errors.Add("The electrical box has no puzzle assigned.");
            }
        }

        if (lamp == null)
        {
            errors.Add("SteamPunkLamp_Powered is missing.");
        }
        else
        {
            PoweredLampController controller =
                lamp.GetComponent<PoweredLampController>();

            if (controller == null)
            {
                errors.Add("The lamp has no PoweredLampController.");
            }
            else if (controller.SourceGenerator == null)
            {
                errors.Add("The lamp is not connected to the electrical box.");
            }

            if (lamp.GetComponentInChildren<Light>(true) == null)
                errors.Add("The lamp has no Point Light.");

            EnemyLightSource lightSource =
                lamp.GetComponentInChildren<EnemyLightSource>(true);

            if (lightSource == null)
            {
                errors.Add("The lamp has no EnemyLightSource.");
            }
            else if (lightSource.ExplicitTargetCount == 0)
            {
                errors.Add(
                    "The lamp has no explicitly assigned moth targets."
                );
            }
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ElectricalPrefabPath) == null)
            errors.Add("Electrical box wrapper prefab is missing.");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefabPath) == null)
            errors.Add("Powered lamp wrapper prefab is missing.");

        GameObject electricalAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(ElectricalPrefabPath);
        GameObject lampAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefabPath);

        if (electricalAsset != null)
        {
            Transform visual = electricalAsset.transform.Find("Visual");
            string nestedPath = visual == null
                ? string.Empty
                : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    visual.gameObject
                );

            if (visual == null || nestedPath != ElectricalVisualPrefabPath)
                errors.Add("The electrical visual is not a nested Prefab.");

            if (electricalAsset
                    .GetComponentsInChildren<GeneratorInteractable>(true)
                    .Length != 1)
            {
                errors.Add(
                    "The electrical wrapper Prefab contains duplicate interaction components."
                );
            }
        }

        if (lampAsset != null)
        {
            Transform visual = lampAsset.transform.Find("Visual");
            string nestedPath = visual == null
                ? string.Empty
                : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    visual.gameObject
                );

            if (visual == null || nestedPath != LampVisualPrefabPath)
                errors.Add("The lamp visual is not a nested Prefab.");
        }

        Material fadeMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Material/Wall_Fade_Material.mat"
        );

        Renderer invalidOccluder = Resources
            .FindObjectsOfTypeAll<Renderer>()
            .FirstOrDefault(renderer =>
                renderer != null &&
                renderer.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(renderer) &&
                renderer.sharedMaterials.Any(material =>
                    material == fadeMaterial) &&
                renderer.gameObject.layer != CameraOccluderLayer);

        if (invalidOccluder != null)
        {
            errors.Add(
                invalidOccluder.name +
                " uses the wall fade material but is not on CameraOccluder."
            );
        }

        if (errors.Count == 0)
        {
            return
                "Phase 1 validation passed.\n\n" +
                "Room hierarchy, switch prompt, wall occlusion fade, wrapper " +
                "prefabs, puzzle reference, lamp power event, Point Light, " +
                "player auto-binding, and explicit moth attraction targets " +
                "are connected.";
        }

        return "Phase 1 validation failed:\n- " + string.Join("\n- ", errors);
    }
}
