namespace Threadlight.Mirroring.Editor
{
using Threadlight.Authoring;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts the released customer-side Live Mirroring authoring component to
/// the current ThreadLight Authoring type without linking Authoring to Components.
/// </summary>
public static class LegacyLiveMirroringBridge
{
    private const string UndoName = "Migrate Live Mirroring Authoring";
    private const string LegacyTypeName =
        "Threadlight.Mirroring.LiveMirroringSystem, " +
        "Threadlight.Components";
    private const string LegacyHierarchyMetadataTypeName =
        "Threadlight.Authoring.GeneratedHierarchyMetadata, " +
        "Threadlight.Components";
    private const string LegacyTargetMetadataTypeName =
        "Threadlight.Authoring.GeneratedTargetMetadata, " +
        "Threadlight.Components";
    // Released Glizzy-era setups used these IDs before the ThreadLight rebrand.
    // They are the only predecessor ownership contract this bridge may adopt.
    private const string LegacyManualOwnerId = "wolfy.live-mirroring.manual";
    private const string LegacyManualModuleId = "wolfy.live-mirroring.targets";
    private static readonly HashSet<string> SerializedContract =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "dataVersion", "liveMirror", "mirrorCenter",
            "constraintTargetsObjectName", "addVrcfuryArmatureLinks",
            "applyScaleReference", "scaleReference", "scaleHandles", "pairs",
            "showScenePreview", "previewSource", "previewMaterial",
            "generateThreadlightComponentsBootstrapper",
            "threadlightComponentsBootstrapperFolderPath", "mirrorOptions"
        };
    private static readonly HashSet<string> HierarchyMetadataContract =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ownerId", "moduleId", "stableId", "role", "createdByBuilder"
        };
    private static readonly HashSet<string> TargetMetadataContract =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "stableId", "role", "displayName", "ownerId", "moduleId",
            "removeGeneratedObject", "createdByBuilder"
        };

    public static bool TryConvert(
        GameObject root,
        out AuthoringLiveMirroringSystem system,
        out string error)
    {
        system = null;
        error = string.Empty;
        if (root == null)
            return Fail("Choose a prefab root before migrating Live Mirroring.",
                out error);
        if (EditorUtility.IsPersistent(root))
            return Fail("Open the prefab in Prefab Mode before migrating Live " +
                "Mirroring.", out error);
        AuthoringLiveMirroringSystem[] current =
            root.GetComponentsInChildren<AuthoringLiveMirroringSystem>(true);
        if (current.Length > 0)
        {
            system = current[0];
            return true;
        }
        if (!TryFindLegacy(root, out Component legacy, out error))
            return false;

        Type legacyType = legacy.GetType();
        int dataVersion = ReadDataVersion(legacyType, legacy);
        if (dataVersion > LiveMirroringMigrationService.CurrentDataVersion)
            return Fail("This Live Mirroring setup was saved by a newer " +
                "ThreadLight Authoring. Update the package before editing it.", out error);

        GameObject holder = null;
        bool legacyIsOnRoot = false;
        List<Component> legacyMetadata = new List<Component>();
        List<Component> currentMetadata = new List<Component>();
        bool useUndo = !Application.isBatchMode;
        List<SerializedState> batchRollback = useUndo
            ? null
            : CaptureExistingMetadata(root);
        int undoGroup = -1;
        try
        {
            if (useUndo)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                Undo.RegisterFullObjectHierarchyUndo(root, UndoName);
            }
            GameObject legacyHolder = legacy.gameObject;
            legacyIsOnRoot = legacyHolder == root;
            Transform oldTransform = legacyHolder.transform;
            Transform parent = legacyIsOnRoot
                ? root.transform
                : oldTransform.parent;
            int sibling = legacyIsOnRoot
                ? 0
                : oldTransform.GetSiblingIndex();
            holder = new GameObject(legacyIsOnRoot
                ? "EditorOnly - Live Mirroring"
                : legacyHolder.name)
            {
                layer = legacyHolder.layer,
                tag = "EditorOnly"
            };
            if (useUndo)
                Undo.RegisterCreatedObjectUndo(holder, UndoName);
            holder.SetActive(legacyHolder.activeSelf);
            holder.transform.SetParent(parent, false);
            holder.transform.SetSiblingIndex(sibling);
            if (!legacyIsOnRoot)
            {
                holder.transform.localPosition = oldTransform.localPosition;
                holder.transform.localRotation = oldTransform.localRotation;
                holder.transform.localScale = oldTransform.localScale;
            }
            system = AddComponent<AuthoringLiveMirroringSystem>(holder, useUndo);
            CopySerializedData(legacy, system, SerializedContract);
            system.SetDataVersion(dataVersion);
            if (!legacyIsOnRoot)
                RemapHolderReferences(system, legacyHolder, holder);
            ConvertLegacyMetadata(
                root, legacyMetadata, currentMetadata, useUndo);
            NormalizeReleasedOwnership(root, useUndo);
            VersionedDataMigrationResult result =
                LiveMirroringMigrationService.TryMigrate(system);
            if (result != VersionedDataMigrationResult.Migrated &&
                result != VersionedDataMigrationResult.UpToDate)
                throw new InvalidOperationException(
                    "The legacy data could not be upgraded through the current schema.");
            EnsureLegacyTargetRootMetadata(
                root, system, currentMetadata, useUndo);
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(system))
                throw new InvalidOperationException(
                    "The legacy setup contains a type that is not installed.");
            if (system == null)
                throw new InvalidOperationException(
                    "Unity removed the migrated authoring holder unexpectedly.");
            if (legacyIsOnRoot)
            {
                // Both legacy and current authoring types remove their whole
                // holder at the upload boundary. A root-level legacy shim
                // therefore cannot remain as a backup without risking the
                // prefab root; remove only that obsolete component.
                DestroyObject(legacy, useUndo);
            }
            else
            {
                MoveReferencedTargetBranches(
                    system, oldTransform, holder.transform);
                EnsureAlive(system, "moving legacy target branches");
                // Retain the original holder as a disabled rollback record.
                // Any unrelated creator components remain on that same object
                // with their serialized data and object references intact;
                // only the obsolete Live Mirroring component is disabled.
                // The holder remains EditorOnly and is stripped on upload.
                legacyHolder.name = "Legacy Backup - " + legacyHolder.name;
                EnsureAlive(system, "renaming the legacy backup");
                if (legacy is Behaviour legacyBehaviour)
                    legacyBehaviour.enabled = false;
            }
            EnsureAlive(system, "disabling the legacy backup");
            for (int index = 0; index < legacyMetadata.Count; index++)
                DestroyObject(legacyMetadata[index], useUndo);
            if (useUndo)
                Undo.CollapseUndoOperations(undoGroup);
            return true;
        }
        catch (Exception exception)
        {
            if (useUndo && undoGroup >= 0)
                Undo.RevertAllDownToGroup(undoGroup);
            else
            {
                for (int index = 0; index < currentMetadata.Count; index++)
                    if (currentMetadata[index] != null)
                        UnityEngine.Object.DestroyImmediate(currentMetadata[index]);
                RestoreExistingMetadata(batchRollback);
                if (holder != null)
                    UnityEngine.Object.DestroyImmediate(holder);
            }
            system = null;
            return Fail("Live Mirroring migration was rolled back safely: " +
                exception.Message, out error);
        }
    }

    private static void EnsureAlive(
        AuthoringLiveMirroringSystem system,
        string operation)
    {
        if (system == null)
            throw new InvalidOperationException(
                "Unity removed the migrated component while " + operation + ".");
    }

    private static void ConvertLegacyMetadata(
        GameObject root,
        List<Component> legacy,
        List<Component> current,
        bool useUndo)
    {
        ConvertLegacyMetadata<CreatorHierarchyMetadata>(
            root,
            LegacyHierarchyMetadataTypeName,
            HierarchyMetadataContract,
            legacy,
            current,
            useUndo);
        ConvertLegacyMetadata<CreatorTargetMetadata>(
            root,
            LegacyTargetMetadataTypeName,
            TargetMetadataContract,
            legacy,
            current,
            useUndo);
    }

    private static void NormalizeReleasedOwnership(
        GameObject root,
        bool useUndo)
    {
        foreach (CreatorHierarchyMetadata marker in
                 root.GetComponentsInChildren<CreatorHierarchyMetadata>(true))
        {
            if (!marker.IsOwnedBy(
                    LegacyManualOwnerId, LegacyManualModuleId))
                continue;
            RecordChange(marker, useUndo);
            marker.Configure(
                LiveMirroringSetupUtility.ManualOwnerId,
                LiveMirroringSetupUtility.ManualModuleId,
                marker.StableId,
                marker.Role,
                marker.CreatedByBuilder);
            EditorUtility.SetDirty(marker);
        }

        foreach (CreatorTargetMetadata marker in
                 root.GetComponentsInChildren<CreatorTargetMetadata>(true))
        {
            if (marker.ownerId != LegacyManualOwnerId ||
                marker.moduleId != LegacyManualModuleId)
                continue;
            RecordChange(marker, useUndo);
            marker.ConfigureIdentity(
                LiveMirroringSetupUtility.ManualOwnerId,
                LiveMirroringSetupUtility.ManualModuleId,
                marker.stableId,
                marker.role,
                marker.displayName);
            // ConfigureIdentity intentionally does not alter cleanup ownership.
            EditorUtility.SetDirty(marker);
        }
    }

    private static void RecordChange(UnityEngine.Object target, bool useUndo)
    {
        if (useUndo)
            Undo.RecordObject(target, UndoName);
    }

    private sealed class SerializedState
    {
        public UnityEngine.Object Target;
        public string Json;
    }

    private static List<SerializedState> CaptureExistingMetadata(GameObject root)
    {
        List<SerializedState> states = new List<SerializedState>();
        void Capture(Component component)
        {
            states.Add(new SerializedState
            {
                Target = component,
                Json = EditorJsonUtility.ToJson(component)
            });
        }
        foreach (CreatorHierarchyMetadata marker in
                 root.GetComponentsInChildren<CreatorHierarchyMetadata>(true))
            Capture(marker);
        foreach (CreatorTargetMetadata marker in
                 root.GetComponentsInChildren<CreatorTargetMetadata>(true))
            Capture(marker);
        return states;
    }

    private static void RestoreExistingMetadata(
        IEnumerable<SerializedState> states)
    {
        if (states == null) return;
        foreach (SerializedState state in states)
        {
            if (state?.Target == null) continue;
            EditorJsonUtility.FromJsonOverwrite(state.Json, state.Target);
            EditorUtility.SetDirty(state.Target);
        }
    }

    /// <summary>
    /// Early released setups predate hierarchy ownership metadata. Recover the
    /// target root only when every target explicitly referenced by the legacy
    /// component resolves beneath the same direct child of the prefab root.
    /// This avoids name-based adoption while allowing old creator assets to
    /// cross the current ownership boundary.
    /// </summary>
    private static void EnsureLegacyTargetRootMetadata(
        GameObject root,
        AuthoringLiveMirroringSystem system,
        ICollection<Component> createdMetadata,
        bool useUndo)
    {
        if (LiveMirroringSetupUtility.FindStandaloneTargetRoot(root) != null)
            return;

        List<Transform> assigned = new List<Transform>();
        foreach (AuthoringLiveMirroringSystem.MirrorPair pair in
                 system.pairs ?? Array.Empty<
                     AuthoringLiveMirroringSystem.MirrorPair>())
        {
            if (pair?.sourceTarget != null)
                assigned.Add(pair.sourceTarget);
            if (pair?.mirroredTarget != null)
                assigned.Add(pair.mirroredTarget);
        }
        foreach (Transform handle in
                 system.scaleHandles ?? Array.Empty<Transform>())
        {
            if (handle != null)
                assigned.Add(handle);
        }
        if (assigned.Count == 0)
            return;

        Transform inferredRoot = DirectChildUnder(
            root.transform, assigned[0]);
        if (inferredRoot == null || assigned.Exists(target =>
                DirectChildUnder(root.transform, target) != inferredRoot))
        {
            throw new InvalidOperationException(
                "The legacy targets do not share one recoverable Constraint " +
                "Targets hierarchy.");
        }

        CreatorHierarchyMetadata marker =
            inferredRoot.GetComponent<CreatorHierarchyMetadata>();
        if (marker == null)
        {
            marker = AddComponent<CreatorHierarchyMetadata>(
                inferredRoot.gameObject, useUndo);
            createdMetadata.Add(marker);
        }
        else if (!string.IsNullOrWhiteSpace(marker.OwnerId) &&
                 !marker.IsOwnedBy(
                     LiveMirroringSetupUtility.ManualOwnerId,
                     LiveMirroringSetupUtility.ManualModuleId))
        {
            throw new InvalidOperationException(
                "The recovered Constraint Targets hierarchy is owned by " +
                "another authoring tool.");
        }

        marker.Configure(
            LiveMirroringSetupUtility.ManualOwnerId,
            LiveMirroringSetupUtility.ManualModuleId,
            AuthoringLiveMirroringSystem.TargetsRootStableId,
            "Constraint Targets",
            true);
        EditorUtility.SetDirty(marker);
    }

    private static Transform DirectChildUnder(
        Transform root,
        Transform descendant)
    {
        if (root == null || descendant == null || descendant == root ||
            !descendant.IsChildOf(root))
        {
            return null;
        }
        Transform current = descendant;
        while (current.parent != null && current.parent != root)
            current = current.parent;
        return current.parent == root ? current : null;
    }

    private static void ConvertLegacyMetadata<T>(
        GameObject root,
        string legacyTypeName,
        HashSet<string> contract,
        List<Component> legacy,
        List<Component> current,
        bool useUndo)
        where T : Component
    {
        Type legacyType = Type.GetType(legacyTypeName, false);
        if (legacyType == null)
            return;
        List<Component> matches = FindAll(root, legacyType);
        for (int index = 0; index < matches.Count; index++)
        {
            Component source = matches[index];
            T target = source.GetComponent<T>();
            if (target == null)
            {
                target = AddComponent<T>(source.gameObject, useUndo);
                current.Add(target);
                CopySerializedData(source, target, contract);
            }
            legacy.Add(source);
        }
    }

    private static T AddComponent<T>(GameObject target, bool useUndo)
        where T : Component =>
        useUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();

    private static void DestroyObject(UnityEngine.Object target, bool useUndo)
    {
        if (useUndo)
            Undo.DestroyObjectImmediate(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static void CopySerializedData(
        Component source,
        Component target,
        HashSet<string> contract)
    {
        SerializedObject sourceObject = new SerializedObject(source);
        SerializedObject targetObject = new SerializedObject(target);
        sourceObject.Update();
        targetObject.Update();
        SerializedProperty property = sourceObject.GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren))
        {
            enterChildren = false;
            if (property.depth != 0 ||
                !contract.Contains(property.propertyPath) ||
                targetObject.FindProperty(property.propertyPath) == null)
                continue;
            CopySerializedPropertyTree(property, targetObject);
        }
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Copies one whitelisted serialized root, including array elements and
    /// nested serializable classes. Unity's root-level copy does not resize a
    /// generic destination array, so legacy target pairs would otherwise be
    /// silently omitted.
    /// </summary>
    private static void CopySerializedPropertyTree(
        SerializedProperty source,
        SerializedObject targetObject)
    {
        SerializedProperty target = targetObject.FindProperty(
            source.propertyPath);
        if (target == null) return;
        if (source.propertyType != SerializedPropertyType.Generic)
        {
            targetObject.CopyFromSerializedProperty(source);
            return;
        }

        if (source.isArray)
            target.arraySize = source.arraySize;

        SerializedProperty iterator = source.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;
        while (iterator.Next(enterChildren) &&
               !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            SerializedProperty destination = targetObject.FindProperty(
                iterator.propertyPath);
            if (destination == null) continue;
            if (iterator.propertyType == SerializedPropertyType.Generic)
            {
                if (iterator.isArray)
                    destination.arraySize = iterator.arraySize;
                enterChildren = true;
                continue;
            }
            targetObject.CopyFromSerializedProperty(iterator);
        }
    }

    private static void RemapHolderReferences(
        AuthoringLiveMirroringSystem system,
        GameObject previous,
        GameObject current)
    {
        Transform oldTransform = previous.transform;
        Transform newTransform = current.transform;
        if (system.mirrorCenter == oldTransform)
            system.mirrorCenter = newTransform;
        if (system.scaleReference == oldTransform)
            system.scaleReference = newTransform;
        if (system.previewSource == previous)
            system.previewSource = current;
        if (system.scaleHandles != null)
            for (int index = 0; index < system.scaleHandles.Length; index++)
                if (system.scaleHandles[index] == oldTransform)
                    system.scaleHandles[index] = newTransform;
        if (system.pairs != null)
            for (int index = 0; index < system.pairs.Length; index++)
            {
                AuthoringLiveMirroringSystem.MirrorPair pair =
                    system.pairs[index];
                if (pair == null)
                    continue;
                if (pair.sourceTarget == oldTransform)
                    pair.sourceTarget = newTransform;
                if (pair.mirroredTarget == oldTransform)
                    pair.mirroredTarget = newTransform;
            }
    }

    /// <summary>
    /// Moves only hierarchy branches explicitly referenced as mirroring
    /// targets. Unrelated creator children stay with the legacy backup rather
    /// than being adopted into ThreadLight's upload-stripped holder.
    /// </summary>
    private static void MoveReferencedTargetBranches(
        AuthoringLiveMirroringSystem system,
        Transform previousHolder,
        Transform currentHolder)
    {
        HashSet<Transform> branches = new HashSet<Transform>();
        void AddBranch(Transform reference)
        {
            if (reference == null || reference == previousHolder ||
                !reference.IsChildOf(previousHolder))
                return;
            Transform branch = reference;
            while (branch.parent != previousHolder)
                branch = branch.parent;
            branches.Add(branch);
        }

        if (system.pairs != null)
            for (int index = 0; index < system.pairs.Length; index++)
            {
                AuthoringLiveMirroringSystem.MirrorPair pair =
                    system.pairs[index];
                if (pair == null) continue;
                AddBranch(pair.sourceTarget);
                AddBranch(pair.mirroredTarget);
            }
        if (system.scaleHandles != null)
            for (int index = 0; index < system.scaleHandles.Length; index++)
                AddBranch(system.scaleHandles[index]);

        foreach (Transform branch in branches)
            branch.SetParent(currentHolder, true);
    }

    public static bool CanConvert(GameObject root, out string error) =>
        TryFindLegacy(root, out _, out error);

    /// <summary>
    /// Repairs the ownership marker omitted by early authoring releases using
    /// only the targets explicitly assigned by that setup. The caller owns the
    /// surrounding Undo transaction.
    /// </summary>
    public static bool TryRecoverTargetRootMetadata(
        GameObject root,
        AuthoringLiveMirroringSystem system,
        out Transform targetRoot,
        out string error)
    {
        targetRoot = null;
        error = null;
        try
        {
            NormalizeReleasedOwnership(root, true);
            EnsureLegacyTargetRootMetadata(
                root,
                system,
                new List<Component>(),
                true);
            targetRoot =
                LiveMirroringSetupUtility.FindStandaloneTargetRoot(root);
            if (targetRoot != null)
                return true;
            error = "The assigned legacy targets do not identify one " +
                    "recoverable Constraint Targets hierarchy.";
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Removes the released authoring shim after another tool has successfully
    /// adopted its data. Unrelated creator components are retained; an empty
    /// backup holder is removed with the same Undo transaction as the import.
    /// </summary>
    public static void CleanupConvertedLegacyBackup(GameObject root)
    {
        if (root == null)
            return;
        Type legacyType = Type.GetType(LegacyTypeName, false);
        if (legacyType == null)
            return;

        foreach (Component legacy in FindAll(root, legacyType))
        {
            if (legacy == null)
                continue;
            GameObject holder = legacy.gameObject;
            Undo.DestroyObjectImmediate(legacy);

            CreatorHierarchyMetadata marker =
                holder.GetComponent<CreatorHierarchyMetadata>();
            if (marker != null && marker.IsOwnedBy(
                    LiveMirroringSetupUtility.ManualOwnerId,
                    LiveMirroringSetupUtility.ManualModuleId))
            {
                Undo.DestroyObjectImmediate(marker);
            }

            if (holder != root && holder.transform.childCount == 0 &&
                holder.GetComponents<Component>().Length == 1)
            {
                Undo.DestroyObjectImmediate(holder);
                continue;
            }

            const string backupPrefix = "Legacy Backup - ";
            if (holder.name.StartsWith(
                    backupPrefix, StringComparison.Ordinal))
            {
                Undo.RecordObject(holder, UndoName);
                holder.name = holder.name.Substring(backupPrefix.Length);
            }
        }
    }

    private static bool TryFindLegacy(
        GameObject root,
        out Component legacy,
        out string error)
    {
        legacy = null;
        if (root == null)
            return Fail("Choose a prefab root before migrating Live Mirroring.",
                out error);
        Type legacyType = Type.GetType(LegacyTypeName, false);
        if (legacyType == null)
            return Fail("No legacy Live Mirroring compatibility component is " +
                "installed.", out error);
        List<Component> matches = Find(root, legacyType);
        if (matches.Count == 0)
            return Fail("No legacy Live Mirroring setup was found on this root.",
                out error);
        if (matches.Count > 1)
            return Fail("More than one legacy Live Mirroring setup was found. " +
                "Migrate each setup from its own prefab root.", out error);
        legacy = matches[0];
        error = string.Empty;
        return true;
    }

    private static List<Component> Find(GameObject root, Type type)
    {
        List<Component> output = new List<Component>();
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int index = 0; index < components.Length; index++)
        {
            Component component = components[index];
            // The original shipped Glizzy nested its authoring holder beneath
            // setup folders. Exact type identity and the single-match check
            // above are the ownership boundary; hierarchy depth is not.
            if (component != null && component.GetType() == type)
                output.Add(component);
        }
        return output;
    }

    private static List<Component> FindAll(GameObject root, Type type)
    {
        List<Component> output = new List<Component>();
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int index = 0; index < components.Length; index++)
            if (components[index] != null && components[index].GetType() == type)
                output.Add(components[index]);
        return output;
    }

    private static int ReadDataVersion(Type type, Component target)
    {
        object value = type.GetProperty("DataVersion")?.GetValue(target, null);
        return value is int version ? version : 0;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
}
