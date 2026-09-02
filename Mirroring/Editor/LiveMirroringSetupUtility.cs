namespace Threadlight.Mirroring.Editor
{
using Threadlight.Authoring;
using Threadlight.Mirroring;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LiveMirroringSetupUtility
{
    private const string PreviewMaterialGuid = "1ea3771d93ec4d48b199f1916cbacb30";
    private static readonly string[] PreviewMaterialPaths =
    {
        "Packages/com.wolfyvr.threadlight.authoring/Assets/Ghost Material.mat",
        "Assets/Threadlight/Components/Fallback/Ghost Material.mat"
    };
    public const string DefaultHolderName = "EditorOnly - Live Mirroring";
    public const string DefaultTargetsName = AuthoringLiveMirroringSystem.DefaultConstraintTargetsObjectName;
    public const string DefaultSourceFolderName = "SETUP - Move These";
    public const string DefaultMirroredFolderName = "AUTO - Mirrored Targets - Do Not Touch";
    public const string DefaultPrefabContainerName = "Prefab Container";
    public const string ManualOwnerId = AuthoringLiveMirroringSystem.StandaloneOwnerId;
    public const string ManualModuleId = AuthoringLiveMirroringSystem.StandaloneModuleId;
    internal const string HolderStableId = AuthoringLiveMirroringSystem.HolderStableId;
    internal const string TargetsRootStableId = AuthoringLiveMirroringSystem.TargetsRootStableId;
    private static Material defaultPreviewMaterial;
    private static bool defaultPreviewMaterialResolved;
    internal static int DefaultPreviewMaterialResolutionCount { get; private set; }

    static LiveMirroringSetupUtility()
    {
        EditorApplication.projectChanged -= InvalidateDefaultPreviewMaterialCache;
        EditorApplication.projectChanged += InvalidateDefaultPreviewMaterialCache;
    }

    public readonly struct BuildResult
    {
        public int CreatedTargets { get; }
        public int RemovedTargets { get; }
        public int RenamedTargets { get; }
        public int AppliedIntegrationChanges { get; }
        public int ReorderedObjects { get; }
        public bool Changed => CreatedTargets + RemovedTargets + RenamedTargets + AppliedIntegrationChanges + ReorderedObjects > 0;
        public BuildResult(int createdTargets, int removedTargets, int renamedTargets,
            int appliedIntegrationChanges = 0, int reorderedObjects = 0)
        {
            CreatedTargets = createdTargets; RemovedTargets = removedTargets; RenamedTargets = renamedTargets;
            AppliedIntegrationChanges = appliedIntegrationChanges; ReorderedObjects = reorderedObjects;
        }
    }

    public static Transform ResolveAuthoringRoot(AuthoringLiveMirroringSystem system) =>
        LiveMirroringService.ResolveAuthoringRoot(system);

    public static bool IsManagedByAnotherTool(AuthoringLiveMirroringSystem system) =>
        TryGetManagingTool(ResolveAuthoringRoot(system)?.gameObject, system, out _);
    public static bool IsRootManagedByAnotherTool(GameObject root, out string managerName) =>
        TryGetManagingTool(root, null, out managerName);

    public static bool TryGetManagingTool(GameObject root, AuthoringLiveMirroringSystem system, out string managerName)
    {
        managerName = null;
        if (system == null && root == null) return false;
        IReadOnlyList<ILiveMirroringSetupOwnershipContributor> contributors;
        try
        {
            contributors = LiveMirroringEditorExtensionRegistry
                .GetSetupOwnershipContributors();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning(
                "Live Mirroring could not verify authoring ownership: " +
                exception.Message + " The setup will remain read-only until " +
                "ownership discovery succeeds.");
            managerName =
                "an authoring extension with unavailable ownership discovery";
            return true;
        }
        LiveMirroringSetupOwnershipContext context = new LiveMirroringSetupOwnershipContext(root, system);
        for (int i = 0; i < contributors.Count; i++)
        {
            try
            {
                if (!contributors[i].Claims(context)) continue;
                managerName = string.IsNullOrWhiteSpace(contributors[i].OwnerDisplayName)
                    ? "another builder" : contributors[i].OwnerDisplayName.Trim();
                return true;
            }
            catch (Exception exception)
            {
                LiveMirroringExtensionHealthJournal.RecordIsolatedFailure(
                    LiveMirroringExtensionCapabilities.SetupOwnership,
                    LiveMirroringEditorExtensionRegistry.GetContributorId(contributors[i]),
                    contributors[i]?.GetType(),
                    "ownership",
                    exception);
                Debug.LogWarning($"Live Mirroring ownership contributor '{contributors[i]?.GetType().FullName ?? "<unknown>"}' " +
                    $"failed: {exception.Message} The setup will remain read-only until the ownership check succeeds.");
                managerName = "an authoring extension with an unavailable ownership check";
                return true;
            }
        }
        CreatorHierarchyMetadata holder = system != null ? system.GetComponent<CreatorHierarchyMetadata>() : null;
        if (holder != null && !holder.IsOwnedBy(ManualOwnerId)) { managerName = "another builder"; return true; }
        if (system != null)
        {
            IReadOnlyList<LiveMirroringPairFact> pairs = LiveMirroringService.Evaluate(system).PairFacts;
            for (int i = 0; i < pairs.Count; i++)
                if (HasExternalOwner(pairs[i].Pair?.sourceTarget) || HasExternalOwner(pairs[i].Pair?.mirroredTarget))
                { managerName = "another builder"; return true; }
        }
        return false;
    }

    public static AuthoringLiveMirroringSystem[] FindForRoot(GameObject root)
    {
        if (root == null) return Array.Empty<AuthoringLiveMirroringSystem>();
        AuthoringLiveMirroringSystem[] systems = root.GetComponentsInChildren<AuthoringLiveMirroringSystem>(true);
        List<AuthoringLiveMirroringSystem> result = new List<AuthoringLiveMirroringSystem>();
        for (int i = 0; i < systems.Length; i++)
            if (systems[i] != null &&
                (systems[i].gameObject == root ||
                 systems[i].transform.parent == root.transform ||
                 (IsStandaloneTargetRoot(systems[i].transform.parent) &&
                  ResolveAuthoringRoot(systems[i]) == root.transform)))
                result.Add(systems[i]);
        return result.ToArray();
    }

    public static bool IsStandaloneOwnedTarget(Transform target) => TargetMarker(target)?.IsOwnedBy(ManualOwnerId) == true &&
        TargetMarker(target).moduleId == ManualModuleId;
    public static bool IsStandaloneOwnedHierarchy(Transform target) =>
        HierarchyMarker(target)?.IsOwnedBy(ManualOwnerId, ManualModuleId) == true;
    public static Transform FindStandaloneTargetRoot(GameObject root)
    {
        if (root == null) return null;
        return TryFindUniqueHierarchy(
            root.transform,
            TargetsRootStableId,
            root.transform,
            out Transform targetRoot)
                ? targetRoot
                : null;
    }

    public static bool TryFindStandaloneSetup(GameObject root, out AuthoringLiveMirroringSystem system, out string error)
    {
        AuthoringLiveMirroringSystem[] systems = FindForRoot(root);
        system = systems.Length == 1 ? systems[0] : null;
        error = systems.Length == 0 ? "No standalone ThreadLight Mirroring setup was found on this root." :
            systems.Length > 1 ? "More than one Live Mirroring setup was found on this root." : null;
        CreatorHierarchyMetadata marker = system != null ? system.GetComponent<CreatorHierarchyMetadata>() : null;
        if (marker != null && !marker.IsOwnedBy(ManualOwnerId, ManualModuleId))
        { system = null; error = "The Live Mirroring setup is owned by another authoring tool."; }
        return system != null;
    }

    public static bool TryCreate(GameObject root, out AuthoringLiveMirroringSystem system, out string error) =>
        TryCreate(root, null, out system, out error);

    public static bool TryCreate(GameObject root, Transform scaleReference, out AuthoringLiveMirroringSystem system, out string error)
    {
        system = null; error = null;
        if (root == null) { error = "Choose a prefab root before creating the setup."; return false; }
        if (EditorUtility.IsPersistent(root)) { error = "Open the prefab in Prefab Mode before creating its setup."; return false; }
        if (IsRootManagedByAnotherTool(root, out string manager))
        { error = $"This root is managed by {manager}. Create or edit its Constraint Targets setup there."; return false; }
        AuthoringLiveMirroringSystem[] existing = FindForRoot(root);
        if (existing.Length > 0)
        { error = existing.Length == 1 ? "This root already contains a Live Mirroring setup." :
            $"This root already contains {existing.Length} Live Mirroring setups."; return false; }
        if (scaleReference != null && !ValidateScaleReferenceForRoot(root, scaleReference, out error)) return false;
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Live Mirroring Setup");
        try
        {
            scaleReference ??= GetOrCreateContainer(root.transform);
            if (!ValidateScaleReferenceForRoot(root, scaleReference, out error))
            { Undo.RevertAllDownToGroup(undo); return false; }
            GameObject holder = new GameObject(DefaultHolderName) { tag = "EditorOnly" };
            Undo.RegisterCreatedObjectUndo(holder, "Create Live Mirroring Setup");
            Undo.SetTransformParent(holder.transform, root.transform, "Parent Live Mirroring Setup");
            Reset(holder.transform);
            system = Undo.AddComponent<AuthoringLiveMirroringSystem>(holder);
            Undo.AddComponent<CreatorHierarchyMetadata>(holder).Configure(
                ManualOwnerId, ManualModuleId, HolderStableId, "Live Mirroring", true);
            system.SetDataVersion(LiveMirroringMigrationService.CurrentDataVersion);
            system.liveMirror = true; system.mirrorCenter = root.transform;
            system.constraintTargetsObjectName = DefaultTargetsName;
            system.targetNamePrefix = "Prefab";
            system.applyScaleReference = true; system.scaleReference = scaleReference;
            system.scaleHandles = Array.Empty<Transform>();
            system.pairs = new[] { DefaultPair("Grip"), DefaultPair("Reverse Grip") };
            system.previewMaterial = GetDefaultPreviewMaterial();
            EnsureMissingPairTargets(system);
            if (!ValidateScaleReferenceForSystem(system, out error))
            { Undo.RevertAllDownToGroup(undo); system = null; return false; }
            Dirty(system); Undo.CollapseUndoOperations(undo); return true;
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undo); system = null;
            error = "Unity could not create the Live Mirroring setup: " + exception.Message;
            return false;
        }
    }

    public static bool EnsureScaleReference(AuthoringLiveMirroringSystem system, out bool changed, out string error)
    {
        changed = false; error = null;
        Transform root = ResolveAuthoringRoot(system);
        if (system == null) { error = "Choose a ThreadLight Mirroring setup first."; return false; }
        if (root == null) { error = "Choose a prefab root before assigning its scale reference."; return false; }
        if (system.scaleReference == null)
        {
            Undo.RecordObject(system, "Create Prefab Container");
            system.scaleReference = GetOrCreateContainer(root); changed = true; Dirty(system);
        }
        return ValidateScaleReferenceForSystem(system, out error);
    }

    public static int ConfigurePrefabContainerConstraint(
        AuthoringLiveMirroringSystem system)
    {
        if (system == null) return 0;
        const string undoName = "Configure Prefab Container Constraint";
        bool changed = false;
        Component constraint = system.prefabContainerParentConstraint;
        bool owned = system.PrefabContainerParentConstraintCreatedByThreadlight;
        Transform container = system.scaleReference;

        // A serialized reference without ThreadLight's ownership flag belongs
        // to the creator. Never configure or remove it; create a separate
        // owned constraint below instead.
        if (constraint != null && !owned)
            constraint = null;

        bool validReference = constraint != null && container != null &&
            constraint.gameObject == container.gameObject &&
            VrcConstraintUtility.IsParentConstraint(constraint);
        if (!validReference)
        {
            if (constraint != null && owned)
            {
                VrcConstraintUtility.RemoveParentConstraint(constraint, undoName);
                changed = true;
            }
            constraint = null;
            owned = false;
        }

        if (!system.addParentConstraintToPrefabContainer || container == null)
        {
            if (constraint != null && owned)
            {
                VrcConstraintUtility.RemoveParentConstraint(constraint, undoName);
                changed = true;
            }
            if (system.prefabContainerParentConstraint != null ||
                system.PrefabContainerParentConstraintCreatedByThreadlight)
            {
                system.prefabContainerParentConstraint = null;
                system.SetPrefabContainerParentConstraintOwnership(false);
                changed = true;
            }
            return changed ? 1 : 0;
        }

        if (constraint == null)
        {
            constraint = VrcConstraintUtility.AddParentConstraint(
                container.gameObject, undoName, true, false, 0f, null,
                false, false, false);
            if (constraint == null)
                throw new InvalidOperationException(
                    "ThreadLight could not create its own prefab-container " +
                    "Parent Constraint without modifying an existing " +
                    "creator-owned constraint.");
            owned = true;
            changed = true;
        }
        else if (!VrcConstraintUtility.IsParentConstraintConfigured(
                     constraint, true, false, 0f, false, false))
        {
            VrcConstraintUtility.AddParentConstraint(
                container.gameObject, undoName, true, false, 0f, null,
                false, false);
            changed = true;
        }

        List<Transform> expected = CollectConstraintSources(system);
        if (!VrcConstraintUtility.TryGetSources(constraint, out List<Transform> actual) ||
            !expected.SequenceEqual(actual))
        {
            if (!VrcConstraintUtility.SetSources(
                    constraint, expected, 0f, undoName))
                throw new InvalidOperationException(
                    "The installed VRChat SDK Parent Constraint layout could " +
                    "not accept generated target sources.");
            changed = true;
        }
        if (system.prefabContainerParentConstraint != constraint ||
            system.PrefabContainerParentConstraintCreatedByThreadlight != owned)
        {
            system.prefabContainerParentConstraint = constraint;
            system.SetPrefabContainerParentConstraintOwnership(owned);
            changed = true;
        }
        if (changed) DirtyGeneratedState(system);
        return changed ? 1 : 0;
    }

    public static bool ValidateScaleReferenceForRoot(GameObject root, Transform reference, out string error)
    {
        error = root == null ? "Choose a prefab root before assigning its scale reference." :
            reference == null ? "Assign the prefab object or content container that should scale with the constraint targets." :
            reference == root.transform ? "The prefab root cannot be the scale reference. Assign a separate prefab object or content container." :
            !reference.IsChildOf(root.transform) ? "The prefab scale reference must be stored inside the selected prefab root." : null;
        return error == null;
    }

    public static bool ValidateScaleReferenceForSystem(AuthoringLiveMirroringSystem system, out string error)
    {
        if (system == null) { error = "Choose a ThreadLight Mirroring setup first."; return false; }
        Transform root = ResolveAuthoringRoot(system);
        if (!ValidateScaleReferenceForRoot(root != null ? root.gameObject : null, system.scaleReference, out error)) return false;
        if (system.scaleReference == system.transform) { error = "The Live Mirroring holder cannot be the scale reference."; return false; }
        if (!LiveMirroringService.HasValidScaleReferenceTopology(system))
        { error = "The prefab scale reference must be separate from the constraint target hierarchy, not a target or an ancestor or child of one."; return false; }
        return true;
    }

    public static int EnsureMissingPairTargets(AuthoringLiveMirroringSystem system) => BuildTargets(system).CreatedTargets;
    public static BuildResult BuildTargets(AuthoringLiveMirroringSystem system)
    {
        if (system == null || system.pairs == null || IsManagedByAnotherTool(system)) return default;
        List<LiveMirroringValidationMessage> messages =
            new List<LiveMirroringValidationMessage>();
        LiveMirroringSetupValidation.CollectAll(system, messages);
        string[] errors = messages
            .Where(message => message != null &&
                message.Severity == LiveMirroringValidationSeverity.Error)
            .Select(message => string.IsNullOrWhiteSpace(message.Message)
                ? message.Title
                : message.Title + ": " + message.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException(
                "Live Mirroring target generation was stopped before " +
                "changing the hierarchy:\n- " +
                string.Join("\n- ", errors));
        Transform root = ResolveAuthoringRoot(system);
        return root != null ? LiveMirroringOwnedHierarchyReconciler.Build(system, root) : default;
    }

    public static Material GetDefaultPreviewMaterial()
    {
        if (defaultPreviewMaterialResolved && (ReferenceEquals(defaultPreviewMaterial, null) || defaultPreviewMaterial != null))
            return defaultPreviewMaterial;
        DefaultPreviewMaterialResolutionCount++;
        string path = AssetDatabase.GUIDToAssetPath(PreviewMaterialGuid);
        defaultPreviewMaterial = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
        for (int i = 0; defaultPreviewMaterial == null && i < PreviewMaterialPaths.Length; i++)
            defaultPreviewMaterial = AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPaths[i]);
        defaultPreviewMaterial ??= AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        defaultPreviewMaterialResolved = true;
        return defaultPreviewMaterial;
    }

    internal static void InvalidateDefaultPreviewMaterialCache()
    { defaultPreviewMaterial = null; defaultPreviewMaterialResolved = false; }

    private static Transform GetOrCreateContainer(Transform root)
    {
        if (!TryFindUniqueHierarchy(
                root,
                AuthoringLiveMirroringSystem.PrefabContainerStableId,
                root,
                out Transform container))
            throw new InvalidOperationException(
                "More than one owned Prefab Container was found under this " +
                "prefab root. Resolve the duplicate ownership metadata before building.");
        return container ?? CreateHierarchy(
            root,
            DefaultPrefabContainerName,
            AuthoringLiveMirroringSystem.PrefabContainerStableId,
            "Prefab Container");
    }

    internal static List<Transform> CollectConstraintSources(
        AuthoringLiveMirroringSystem system)
    {
        List<Transform> sources = new List<Transform>();
        if (system?.pairs == null) return sources;
        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
            if (pair?.sourceTarget != null && !sources.Contains(pair.sourceTarget))
                sources.Add(pair.sourceTarget);
            if (system.ShouldCreateOppositeTarget(pair) &&
                pair.mirroredTarget != null &&
                !sources.Contains(pair.mirroredTarget))
                sources.Add(pair.mirroredTarget);
        }
        return sources;
    }

    private static bool TryFindUniqueHierarchy(
        Transform root,
        string stableId,
        Transform requiredParent,
        out Transform result)
    {
        result = null;
        if (root == null) return true;
        CreatorHierarchyMetadata[] markers = root.GetComponentsInChildren<CreatorHierarchyMetadata>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            CreatorHierarchyMetadata marker = markers[i];
            if (marker == null ||
                (requiredParent != null && marker.transform.parent != requiredParent) ||
                !marker.Matches(ManualOwnerId, ManualModuleId, stableId))
                continue;
            if (result != null && result != marker.transform)
            {
                result = null;
                return false;
            }
            result = marker.transform;
        }
        return true;
    }

    private static bool IsStandaloneTargetRoot(Transform target)
    {
        CreatorHierarchyMetadata marker = HierarchyMarker(target);
        return marker != null && marker.Matches(
            ManualOwnerId, ManualModuleId, TargetsRootStableId);
    }

    private static Transform CreateHierarchy(Transform parent, string name, string id, string display)
    {
        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create " + display);
        Undo.SetTransformParent(created.transform, parent, "Parent " + display);
        Reset(created.transform);
        Undo.AddComponent<CreatorHierarchyMetadata>(created).Configure(ManualOwnerId, ManualModuleId, id, display, true);
        return created.transform;
    }

    private static CreatorTargetMetadata TargetMarker(Transform target) =>
        target != null ? target.GetComponent<CreatorTargetMetadata>() : null;
    private static CreatorHierarchyMetadata HierarchyMarker(Transform target) =>
        target != null ? target.GetComponent<CreatorHierarchyMetadata>() : null;
    internal static bool IsOwnedGeneratedTarget(Transform target) => IsStandaloneOwnedTarget(target);
    private static bool HasExternalOwner(Transform target)
    {
        CreatorTargetMetadata marker = TargetMarker(target);
        return marker != null && !marker.IsUnclaimed && !marker.IsOwnedBy(ManualOwnerId);
    }
    private static AuthoringLiveMirroringSystem.MirrorPair DefaultPair(string name) =>
        new AuthoringLiveMirroringSystem.MirrorPair
        {
            mirrorEnabled = true,
            createOppositeTarget = true,
            pairName = name,
            useGlobalSideLabels = true
        };
    private static void Reset(Transform target)
    { target.localPosition = Vector3.zero; target.localRotation = Quaternion.identity; target.localScale = Vector3.one; }
    internal static void DirtyGeneratedState(AuthoringLiveMirroringSystem system)
    {
        EditorUtility.SetDirty(system); PrefabUtility.RecordPrefabInstancePropertyModifications(system);
        if (system.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
    }

    private static void Dirty(AuthoringLiveMirroringSystem system) => DirtyGeneratedState(system);
}
}
