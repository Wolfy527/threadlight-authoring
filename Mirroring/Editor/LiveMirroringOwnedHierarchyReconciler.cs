namespace Threadlight.Mirroring.Editor
{
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Threadlight.Authoring;
using Threadlight.Mirroring;

internal sealed class LiveMirroringOwnedHierarchyReconciler
{
    private const string TargetsRootId = LiveMirroringSetupUtility.TargetsRootStableId;
    private const string SourceFolderId = AuthoringLiveMirroringSystem.SourceFolderStableId;
    private const string OppositeFolderId = AuthoringLiveMirroringSystem.MirroredFolderStableId;

    private readonly struct HierarchySpec
    {
        internal readonly string StableId;
        internal readonly string Name;
        internal readonly string Role;

        internal HierarchySpec(string stableId, string name, string role)
        { StableId = stableId; Name = name; Role = role; }
    }

    private readonly struct PairSpec
    {
        internal readonly AuthoringLiveMirroringSystem.MirrorPair Pair;
        internal readonly int Index;
        internal readonly string SourceName;
        internal readonly string OppositeName;
        internal readonly bool CreateOpposite;

        internal PairSpec(AuthoringLiveMirroringSystem system,
            AuthoringLiveMirroringSystem.MirrorPair pair, int index)
        {
            Pair = pair;
            Index = index;
            SourceName = TargetName(system, pair, index,
                ResolveSideLabel(system, pair, false));
            OppositeName = TargetName(system, pair, index,
                ResolveSideLabel(system, pair, true));
            CreateOpposite = system?.ShouldCreateOppositeTarget(pair) == true;
        }
    }

    private sealed class BuildPlan
    {
        internal readonly PairSpec[] Pairs;

        internal BuildPlan(AuthoringLiveMirroringSystem system)
        {
            Pairs = new PairSpec[system.pairs.Length];
            for (int i = 0; i < Pairs.Length; i++)
                Pairs[i] = new PairSpec(system, system.pairs[i], i);
        }
    }

    private sealed class OwnershipIndex
    {
        private readonly Dictionary<string, List<CreatorHierarchyMetadata>> hierarchy =
            new Dictionary<string, List<CreatorHierarchyMetadata>>();
        private readonly Dictionary<string, List<CreatorTargetMetadata>> targetsByStableId =
            new Dictionary<string, List<CreatorTargetMetadata>>();
        internal readonly List<CreatorTargetMetadata> Targets = new List<CreatorTargetMetadata>();
        internal readonly bool HasCompetingSystem;

        internal OwnershipIndex(AuthoringLiveMirroringSystem system, Transform root)
        {
            CreatorAuthoringComponent[] components =
                root.GetComponentsInChildren<CreatorAuthoringComponent>(true);
            List<Transform> nestedScopes = new List<Transform>();
            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is AuthoringLiveMirroringSystem other) || other == system) continue;
                Transform otherRoot = LiveMirroringSetupUtility.ResolveAuthoringRoot(other);
                if (otherRoot == root) HasCompetingSystem = true;
                else if (otherRoot != null && otherRoot.IsChildOf(root)) nestedScopes.Add(otherRoot);
            }
            for (int i = 0; i < components.Length; i++)
            {
                if (IsInsideNestedScope(components[i].transform, nestedScopes)) continue;
                if (components[i] is CreatorHierarchyMetadata hierarchyMarker &&
                    hierarchyMarker.IsOwnedBy(
                        LiveMirroringSetupUtility.ManualOwnerId,
                        LiveMirroringSetupUtility.ManualModuleId))
                {
                    AddHierarchy(hierarchyMarker);
                }
                else if (components[i] is CreatorTargetMetadata targetMarker &&
                         IsOwned(targetMarker))
                {
                    AddTarget(targetMarker);
                }
            }
        }

        private static bool IsInsideNestedScope(Transform target, IReadOnlyList<Transform> scopes)
        {
            for (int i = 0; i < scopes.Count; i++)
                if (target == scopes[i] || target.IsChildOf(scopes[i])) return true;
            return false;
        }

        internal void AddHierarchy(CreatorHierarchyMetadata marker)
        {
            if (marker == null || string.IsNullOrWhiteSpace(marker.StableId)) return;
            if (!hierarchy.TryGetValue(marker.StableId, out List<CreatorHierarchyMetadata> matches))
                hierarchy.Add(marker.StableId, matches = new List<CreatorHierarchyMetadata>());
            matches.Add(marker);
        }

        internal void AddTarget(CreatorTargetMetadata marker)
        {
            if (marker == null) return;
            Targets.Add(marker);
            if (string.IsNullOrWhiteSpace(marker.stableId)) return;
            if (!targetsByStableId.TryGetValue(marker.stableId, out List<CreatorTargetMetadata> matches))
                targetsByStableId.Add(marker.stableId, matches = new List<CreatorTargetMetadata>());
            matches.Add(marker);
        }

        internal CreatorHierarchyMetadata ResolveHierarchy(
            string stableId, Transform requiredParent, IReadOnlyList<Transform> anchors,
            out bool ambiguous)
        {
            ambiguous = false;
            if (!hierarchy.TryGetValue(stableId, out List<CreatorHierarchyMetadata> stored))
                return null;

            List<CreatorHierarchyMetadata> matches = new List<CreatorHierarchyMetadata>();
            for (int i = 0; i < stored.Count; i++)
                if (stored[i] != null &&
                    (requiredParent == null || stored[i].transform.parent == requiredParent))
                    matches.Add(stored[i]);
            if (matches.Count == 0) return null;
            if (matches.Count == 1) return matches[0];

            CreatorHierarchyMetadata anchored = null;
            for (int i = 0; i < matches.Count; i++)
            {
                if (!ContainsAny(matches[i].transform, anchors)) continue;
                if (anchored != null) { ambiguous = true; return null; }
                anchored = matches[i];
            }
            if (anchored != null) return anchored;
            ambiguous = true;
            return null;
        }

        internal bool IsAmbiguous(CreatorTargetMetadata marker)
        {
            return marker == null || string.IsNullOrWhiteSpace(marker.stableId) ||
                   (targetsByStableId.TryGetValue(marker.stableId, out List<CreatorTargetMetadata> matches) &&
                    matches.Count > 1);
        }

        private static bool ContainsAny(Transform owner, IReadOnlyList<Transform> anchors)
        {
            if (owner == null || anchors == null) return false;
            for (int i = 0; i < anchors.Count; i++)
                if (anchors[i] != null &&
                    (anchors[i] == owner || anchors[i].IsChildOf(owner))) return true;
            return false;
        }
    }

    private readonly AuthoringLiveMirroringSystem system;
    private readonly Transform root;
    private readonly OwnershipIndex index;
    private readonly IReadOnlyList<ILiveMirroringTargetBuildContributor> contributors;
    private readonly HashSet<string> warnedAmbiguities = new HashSet<string>();
    private Transform targetRoot;
    private int created;
    private int removed;
    private int renamed;
    private int integrations;
    private int reordered;

    private LiveMirroringOwnedHierarchyReconciler(AuthoringLiveMirroringSystem system, Transform root)
    {
        this.system = system;
        this.root = root;
        index = new OwnershipIndex(system, root);
        contributors = LiveMirroringEditorExtensionRegistry.GetTargetBuildContributors();
    }

    internal static LiveMirroringSetupUtility.BuildResult Build(
        AuthoringLiveMirroringSystem system, Transform root)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Live Mirroring Targets");
        try
        {
            LiveMirroringSetupUtility.BuildResult result =
                new LiveMirroringOwnedHierarchyReconciler(system, root)
                    .Apply(new BuildPlan(system));
            Undo.CollapseUndoOperations(undoGroup);
            return result;
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private LiveMirroringSetupUtility.BuildResult Apply(BuildPlan plan)
    {
        if (index.HasCompetingSystem)
        {
            Debug.LogWarning(
                "Live Mirroring found more than one standalone setup for this authoring root and left it unchanged.",
                system);
            return default;
        }
        if (HasAmbiguousHierarchy())
            return default;
        EnsureHolderOwnership();
        Undo.RegisterCompleteObjectUndo(system, "Generate Live Mirroring Targets");
        for (int i = 0; i < plan.Pairs.Length; i++)
        {
            PairSpec spec = plan.Pairs[i];
            if (spec.Pair == null) continue;
            spec.Pair.sourceTarget = EnsureTarget(
                spec.Pair.sourceTarget, spec.SourceName,
                CreatorTargetMetadata.TargetRole.Source);
            if (spec.CreateOpposite)
                spec.Pair.mirroredTarget = EnsureTarget(
                    spec.Pair.mirroredTarget, spec.OppositeName,
                    CreatorTargetMetadata.TargetRole.Opposite);
            ApplyContributors(spec.Pair, spec.Index);
        }

        if (system.removeUnusedGeneratedTargets) ReconcileTargets();
        integrations += LiveMirroringSetupUtility
            .ConfigurePrefabContainerConstraint(system);
        OrderHolder();
        FinishHierarchy();
        if (created + removed + renamed + reordered > 0)
            LiveMirroringSetupUtility.DirtyGeneratedState(system);
        return new LiveMirroringSetupUtility.BuildResult(
            created, removed, renamed, integrations, reordered);
    }

    private bool HasAmbiguousHierarchy()
    {
        if (IsAmbiguousHierarchy(
                TargetsRootId, AllPairTargets()))
            return true;
        if (IsAmbiguousHierarchy(
                SourceFolderId,
                RoleAnchors(CreatorTargetMetadata.TargetRole.Source)))
            return true;
        return IsAmbiguousHierarchy(
            OppositeFolderId,
            RoleAnchors(CreatorTargetMetadata.TargetRole.Opposite));
    }

    private bool IsAmbiguousHierarchy(
        string stableId,
        IReadOnlyList<Transform> anchors)
    {
        index.ResolveHierarchy(
            stableId, null, anchors, out bool ambiguous);
        if (!ambiguous) return false;
        WarnAmbiguous("hierarchy:" + stableId);
        return true;
    }

    private void EnsureHolderOwnership()
    {
        CreatorHierarchyMetadata marker =
            system.GetComponent<CreatorHierarchyMetadata>();
        if (marker != null) return;
        marker = Undo.AddComponent<CreatorHierarchyMetadata>(system.gameObject);
        marker.Configure(
            LiveMirroringSetupUtility.ManualOwnerId,
            LiveMirroringSetupUtility.ManualModuleId,
            LiveMirroringSetupUtility.HolderStableId,
            "Live Mirroring",
            true);
    }

    private Transform EnsureTarget(
        Transform existingTarget, string desiredName,
        CreatorTargetMetadata.TargetRole role)
    {
        if (existingTarget != null)
        {
            RenameFromLedger(existingTarget, desiredName);
            CreatorTargetMetadata existingMarker =
                existingTarget.GetComponent<CreatorTargetMetadata>();
            if (system.applyDefaultTransformToExistingTargets &&
                IsOwned(existingMarker) && existingMarker.CreatedByBuilder)
            {
                Undo.RecordObject(existingTarget, "Apply Constraint Target Defaults");
                ApplyTargetDefaults(existingTarget);
            }
            return existingTarget;
        }

        Transform folder = ResolveOrCreateFolder(role);
        if (folder == null) return null;
        GameObject createdObject = new GameObject(desiredName);
        Undo.RegisterCreatedObjectUndo(createdObject, "Create Live Mirroring Target");
        Undo.SetTransformParent(createdObject.transform, folder, "Parent Live Mirroring Target");
        ApplyTargetDefaults(createdObject.transform);
        CreatorTargetMetadata marker = Undo.AddComponent<CreatorTargetMetadata>(createdObject);
        marker.ConfigureIdentity(
            LiveMirroringSetupUtility.ManualOwnerId,
            LiveMirroringSetupUtility.ManualModuleId,
            Guid.NewGuid().ToString("N"), role, desiredName);
        marker.ConfigureCleanup(false);
        marker.ConfigureOwnership(true);
        index.AddTarget(marker);
        created++;
        return createdObject.transform;
    }

    private void RenameFromLedger(Transform target, string desiredName)
    {
        CreatorTargetMetadata marker = target.GetComponent<CreatorTargetMetadata>();
        if (!IsOwned(marker) || target.name == desiredName ||
            string.IsNullOrWhiteSpace(marker.displayName) ||
            target.name != marker.displayName) return;

        Undo.RecordObjects(new UnityEngine.Object[] { target.gameObject, marker },
            "Rename Constraint Target");
        target.name = desiredName;
        marker.ConfigureIdentity(
            marker.ownerId, marker.moduleId, marker.stableId, marker.role, desiredName);
        renamed++;
    }

    private void ApplyContributors(AuthoringLiveMirroringSystem.MirrorPair pair, int pairIndex)
    {
        LiveMirroringEditorExtensionRegistry.DispatchMutationCriticalFailClosed(
            contributors,
            contributor => integrations += Mathf.Max(0, contributor.Apply(
                new LiveMirroringTargetBuildContext(system, pair, pairIndex))));
    }

    private void ReconcileTargets()
    {
        HashSet<Transform> active = ActiveTargets();
        CreatorTargetMetadata[] owned = index.Targets.ToArray();
        for (int i = 0; i < owned.Length; i++)
        {
            CreatorTargetMetadata marker = owned[i];
            if (marker == null || active.Contains(marker.transform)) continue;
            if (index.IsAmbiguous(marker))
            {
                WarnAmbiguous("target:" + (marker.stableId ?? "<missing>"));
                continue;
            }

            if (CanDeletePristineTarget(marker))
                Undo.DestroyObjectImmediate(marker.gameObject);
            else
                Undo.DestroyObjectImmediate(marker);
            removed++;
        }
    }

    private HashSet<Transform> ActiveTargets()
    {
        HashSet<Transform> active = new HashSet<Transform>();
        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
            if (pair == null) continue;
            if (pair.sourceTarget != null) active.Add(pair.sourceTarget);
            if (system.ShouldCreateOppositeTarget(pair) &&
                pair.mirroredTarget != null)
                active.Add(pair.mirroredTarget);
        }
        return active;
    }

    private Transform ResolveOrCreateFolder(CreatorTargetMetadata.TargetRole role)
    {
        Transform targets = ResolveOrCreateTargetRoot();
        if (targets == null) return null;
        bool source = role == CreatorTargetMetadata.TargetRole.Source;
        HierarchySpec spec = new HierarchySpec(
            source ? SourceFolderId : OppositeFolderId,
            source ? SourceFolderName(system) : MirroredFolderName(system),
            source ? "Source Targets" : "Mirrored Targets");
        CreatorHierarchyMetadata marker = index.ResolveHierarchy(
            spec.StableId, null, RoleAnchors(role), out bool ambiguous);
        if (ambiguous) { WarnAmbiguous("hierarchy:" + spec.StableId); return null; }
        return marker != null ? marker.transform : CreateHierarchy(targets, spec);
    }

    private Transform ResolveOrCreateTargetRoot()
    {
        if (targetRoot != null) return targetRoot;
        HierarchySpec spec = new HierarchySpec(
            TargetsRootId, TargetsName(system), "Live Mirroring Targets");
        CreatorHierarchyMetadata marker = index.ResolveHierarchy(
            spec.StableId, null, AllPairTargets(), out bool ambiguous);
        if (ambiguous) { WarnAmbiguous("hierarchy:" + spec.StableId); return null; }
        targetRoot = marker != null ? marker.transform : CreateHierarchy(root, spec);
        return targetRoot;
    }

    private Transform CreateHierarchy(Transform parent, HierarchySpec spec)
    {
        GameObject createdObject = new GameObject(spec.Name);
        Undo.RegisterCreatedObjectUndo(createdObject, "Create " + spec.Role);
        Undo.SetTransformParent(createdObject.transform, parent, "Parent " + spec.Role);
        Reset(createdObject.transform);
        CreatorHierarchyMetadata marker =
            Undo.AddComponent<CreatorHierarchyMetadata>(createdObject);
        marker.Configure(
            LiveMirroringSetupUtility.ManualOwnerId,
            LiveMirroringSetupUtility.ManualModuleId,
            spec.StableId, spec.Role, true);
        index.AddHierarchy(marker);
        return createdObject.transform;
    }

    private void FinishHierarchy()
    {
        Transform resolvedRoot = ResolveExistingTargetRoot();
        if (resolvedRoot == null) return;
        targetRoot = resolvedRoot;
        string desiredName = TargetsName(system);
        if (targetRoot.name != desiredName)
        {
            Undo.RecordObject(targetRoot.gameObject, "Rename Constraint Targets");
            targetRoot.name = desiredName;
            renamed++;
        }

        RemoveEmptyFolder(SourceFolderId, SourceFolderName(system));
        RemoveEmptyFolder(OppositeFolderId, MirroredFolderName(system));
        if (targetRoot == null || targetRoot.childCount != 0) return;
        CreatorHierarchyMetadata marker = targetRoot.GetComponent<CreatorHierarchyMetadata>();
        if (CanDeletePristineHierarchy(marker, root, desiredName))
            Undo.DestroyObjectImmediate(targetRoot.gameObject);
    }

    private Transform ResolveExistingTargetRoot()
    {
        CreatorHierarchyMetadata marker = index.ResolveHierarchy(
            TargetsRootId, null, AllPairTargets(), out bool ambiguous);
        if (ambiguous) { WarnAmbiguous("hierarchy:" + TargetsRootId); return null; }
        return marker != null ? marker.transform : targetRoot;
    }

    private void RemoveEmptyFolder(string stableId, string expectedName)
    {
        CreatorHierarchyMetadata marker = index.ResolveHierarchy(
            stableId, null, AllPairTargets(), out bool ambiguous);
        if (ambiguous) { WarnAmbiguous("hierarchy:" + stableId); return; }
        if (marker != null && marker.transform.childCount == 0 &&
            CanDeletePristineHierarchy(marker, targetRoot, expectedName))
            Undo.DestroyObjectImmediate(marker.gameObject);
    }

    private IReadOnlyList<Transform> RoleAnchors(CreatorTargetMetadata.TargetRole role)
    {
        List<Transform> anchors = new List<Transform>();
        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
            if (pair == null) continue;
            Transform target = role == CreatorTargetMetadata.TargetRole.Source
                ? pair.sourceTarget
                : system.ShouldCreateOppositeTarget(pair)
                    ? pair.mirroredTarget
                    : null;
            if (target != null) anchors.Add(target);
        }
        return anchors;
    }

    private IReadOnlyList<Transform> AllPairTargets()
    {
        List<Transform> anchors = new List<Transform>();
        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
            if (pair == null) continue;
            if (pair.sourceTarget != null) anchors.Add(pair.sourceTarget);
            if (system.ShouldCreateOppositeTarget(pair) &&
                pair.mirroredTarget != null)
                anchors.Add(pair.mirroredTarget);
        }
        return anchors;
    }

    private bool CanDeletePristineTarget(CreatorTargetMetadata marker)
    {
        if (marker == null || !marker.CreatedByBuilder ||
            marker.transform.childCount > 0 || marker.transform.name != marker.displayName ||
            !HasConfiguredTargetTransform(marker.transform) ||
            !HasCanonicalGeneratedParent(marker)) return false;
        Component link = LiveMirroringArmatureLinkUtility.GetExistingComponent(marker.gameObject);
        Component[] components = marker.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] == null ||
                (!(components[i] is Transform) && components[i] != marker && components[i] != link))
                return false;
        return true;
    }

    private bool HasConfiguredTargetTransform(Transform target) =>
        target != null && target.localPosition == system.targetLocalPosition &&
        Quaternion.Angle(target.localRotation,
            Quaternion.Euler(system.targetLocalEulerRotation)) < .001f &&
        target.localScale == system.targetLocalScale;

    private bool HasCanonicalGeneratedParent(CreatorTargetMetadata marker)
    {
        Transform folder = marker.transform.parent;
        CreatorHierarchyMetadata folderMarker =
            folder != null ? folder.GetComponent<CreatorHierarchyMetadata>() : null;
        string folderId = marker.role == CreatorTargetMetadata.TargetRole.Source
            ? SourceFolderId : OppositeFolderId;
        if (folderMarker == null || !folderMarker.CreatedByBuilder ||
            !folderMarker.Matches(
                LiveMirroringSetupUtility.ManualOwnerId,
                LiveMirroringSetupUtility.ManualModuleId, folderId) ||
            !IsDefaultTransform(folder)) return false;
        Transform targets = folder.parent;
        CreatorHierarchyMetadata targetsMarker =
            targets != null ? targets.GetComponent<CreatorHierarchyMetadata>() : null;
        return targetsMarker != null && targetsMarker.CreatedByBuilder &&
               targetsMarker.Matches(
                   LiveMirroringSetupUtility.ManualOwnerId,
                   LiveMirroringSetupUtility.ManualModuleId, TargetsRootId) &&
               targets.parent == root && IsDefaultTransform(targets);
    }

    private static bool CanDeletePristineHierarchy(
        CreatorHierarchyMetadata marker, Transform expectedParent, string expectedName)
    {
        if (marker == null || !marker.CreatedByBuilder || marker.transform.parent != expectedParent ||
            marker.transform.name != expectedName || !IsDefaultTransform(marker.transform)) return false;
        Component[] components = marker.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] == null ||
                (!(components[i] is Transform) && components[i] != marker)) return false;
        return true;
    }

    private void OrderHolder()
    {
        Transform holder = system.transform;
        Transform parent = targetRoot != null
            ? targetRoot
            : ResolveOrCreateTargetRoot();
        if (parent == null) return;
        if (holder.parent == parent && holder.GetSiblingIndex() == parent.childCount - 1) return;
        Transform previousParent = holder.parent;
        Undo.RegisterCompleteObjectUndo(
            previousParent == null || previousParent == parent
                ? new UnityEngine.Object[] { parent, holder }
                : new UnityEngine.Object[] { previousParent, parent, holder },
            "Place Live Mirroring Setup");
        if (holder.parent != parent)
        {
            // A null center historically mirrored around the holder's parent.
            // Capture that effective root before nesting the holder so this
            // hierarchy normalization cannot change existing behavior.
            if (system.mirrorCenter == null)
            {
                Undo.RecordObject(system, "Preserve Live Mirroring Center");
                system.mirrorCenter = root;
            }
            Undo.SetTransformParent(holder, parent, "Place Live Mirroring Setup");
        }
        holder.SetAsLastSibling();
        reordered++;
    }

    private void WarnAmbiguous(string key)
    {
        if (!warnedAmbiguities.Add(key)) return;
        Debug.LogWarning(
            $"Live Mirroring retained ambiguous owned metadata '{key}' and left that hierarchy branch unchanged.",
            system);
    }

    private static bool IsOwned(CreatorTargetMetadata marker) =>
        marker != null && marker.IsOwnedBy(LiveMirroringSetupUtility.ManualOwnerId) &&
        marker.moduleId == LiveMirroringSetupUtility.ManualModuleId;

    private static bool IsDefaultTransform(Transform target) =>
        target != null && target.localPosition == Vector3.zero &&
        target.localRotation == Quaternion.identity && target.localScale == Vector3.one;

    private static void Reset(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private void ApplyTargetDefaults(Transform target)
    {
        if (target == null) return;
        target.localPosition = system.targetLocalPosition;
        target.localRotation = Quaternion.Euler(system.targetLocalEulerRotation);
        target.localScale = system.targetLocalScale;
    }

    private static string TargetsName(AuthoringLiveMirroringSystem value) =>
        string.IsNullOrWhiteSpace(value?.constraintTargetsObjectName)
            ? LiveMirroringSetupUtility.DefaultTargetsName
            : value.constraintTargetsObjectName.Trim();

    private static string SourceFolderName(AuthoringLiveMirroringSystem value) =>
        string.IsNullOrWhiteSpace(value?.sourceFolderName)
            ? LiveMirroringSetupUtility.DefaultSourceFolderName
            : value.sourceFolderName.Trim();

    private static string MirroredFolderName(AuthoringLiveMirroringSystem value) =>
        string.IsNullOrWhiteSpace(value?.mirroredFolderName)
            ? LiveMirroringSetupUtility.DefaultMirroredFolderName
            : value.mirroredFolderName.Trim();

    private static string TargetName(
        AuthoringLiveMirroringSystem system,
        AuthoringLiveMirroringSystem.MirrorPair pair,
        int index,
        string side)
    {
        string result = string.IsNullOrWhiteSpace(pair?.pairName)
            ? $"Target {index + 1}"
            : pair.pairName.Trim();
        if (!string.IsNullOrWhiteSpace(system?.targetNamePrefix))
            result = system.targetNamePrefix.Trim() + " " + result;
        if (!string.IsNullOrWhiteSpace(side)) result += " " + side.Trim();
        return result;
    }

    private static string ResolveSideLabel(
        AuthoringLiveMirroringSystem system,
        AuthoringLiveMirroringSystem.MirrorPair pair,
        bool mirrored)
    {
        if (pair?.useGlobalSideLabels != false)
            return mirrored ? system?.mirroredSideLabel : system?.sourceSideLabel;
        return mirrored ? pair.mirroredSideLabel : pair.sourceSideLabel;
    }
}
}
