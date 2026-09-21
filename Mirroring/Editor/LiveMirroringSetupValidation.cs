namespace Threadlight.Mirroring.Editor {
using Threadlight.Authoring;
using Threadlight.Mirroring;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class LiveMirroringDiagnostics {
    internal readonly List<LiveMirroringValidationMessage> Messages =
        new List<LiveMirroringValidationMessage>();
    private readonly List<Component> components = new List<Component>();
    private readonly HashSet<string> failedValidators = new HashSet<string>();
    private int extensionGeneration = -1;
    internal int Errors { get; private set; }
    internal int Warnings { get; private set; }
    internal bool HasBlockingErrors { get; private set; }

    internal void CollectReport(SerializedObject serializedSystem) {
        Messages.Clear();
        int generation = LiveMirroringEditorExtensionRegistry.Generation;
        if (extensionGeneration != generation) {
            failedValidators.Clear();
            extensionGeneration = generation;
        }
        LiveMirroringSetupValidation.CollectAll(
            serializedSystem, Messages, failedValidators, components);
        HasBlockingErrors = ContainsErrors(Messages);
        Count();
    }

    private void Count() {
        Errors = Warnings = 0;
        for (int i = 0; i < Messages.Count; i++) {
            if (Messages[i]?.Severity == LiveMirroringValidationSeverity.Error) Errors++;
            else if (Messages[i]?.Severity == LiveMirroringValidationSeverity.Warning) Warnings++;
        }
    }

    private static bool ContainsErrors(
        IReadOnlyList<LiveMirroringValidationMessage> messages) {
        for (int i = 0; i < messages.Count; i++)
            if (messages[i]?.Severity == LiveMirroringValidationSeverity.Error)
                return true;
        return false;
    }
}

public static class LiveMirroringSetupValidation {
    public static bool HasBlockingErrors(AuthoringLiveMirroringSystem system) {
        List<LiveMirroringValidationMessage> messages =
            new List<LiveMirroringValidationMessage>();
        CollectAll(system, messages);
        for (int i = 0; i < messages.Count; i++)
            if (messages[i]?.Severity == LiveMirroringValidationSeverity.Error)
                return true;
        return false;
    }
    public static void Collect(
        AuthoringLiveMirroringSystem system,
        List<LiveMirroringValidationMessage> messages) {
        Collect(system, messages, null);
    }
    public static void CollectAll(
        AuthoringLiveMirroringSystem system,
        List<LiveMirroringValidationMessage> messages) {
        if (system == null || messages == null)
            return;
        CollectAll(
            new SerializedObject(system),
            messages,
            new HashSet<string>(),
            null);
    }
    internal static void CollectAll(
        SerializedObject serializedSystem,
        List<LiveMirroringValidationMessage> messages,
        HashSet<string> failedValidators,
        List<Component> componentBuffer) {
        AuthoringLiveMirroringSystem system =
            serializedSystem?.targetObject as AuthoringLiveMirroringSystem;
        if (system == null || messages == null)
            return;
        if (!HasSupportedDataVersion(system, messages)) {
            LiveMirroringExtensionHealthDiagnostics.Append(messages);
            return;
        }
        Collect(system, messages, componentBuffer);
        LiveMirroringEditorExtensionRegistry.DispatchOptionalIsolated(
            LiveMirroringEditorExtensionRegistry.GetValidators(),
            failedValidators ?? new HashSet<string>(),
            LiveMirroringExtensionCapabilities.Validation,
            system,
            contributor => contributor.Validate(serializedSystem, messages));
        LiveMirroringExtensionHealthDiagnostics.Append(messages);
    }
    internal static void Collect(
        AuthoringLiveMirroringSystem system,
        List<LiveMirroringValidationMessage> messages,
        List<Component> componentBuffer) {
        if (system == null || messages == null)
            return;
        if (!HasSupportedDataVersion(system, messages))
            return;
        LiveMirroringEvaluationBuffers graph = LiveMirroringService.AnalyzePairs(system);
        if (LiveMirroringService.HasAmbiguousAuthoringRoot(system))
            Add(messages,
                LiveMirroringValidationSeverity.Error,
                "Multiple Setups Control This Prefab",
                "More than one Live Mirroring setup controls this Prefab Root. Keep one setup or move each additional setup to a different Prefab Root.",
                "@setup");
        if (!system.gameObject.CompareTag("EditorOnly"))
            Add(messages,
                LiveMirroringValidationSeverity.Warning,
                "Setup Holder Is Not EditorOnly",
                "Move the setup to a dedicated EditorOnly object. ThreadLight removes that object during Play Mode and avatar upload while preserving its children.",
                "@setup");
        if (system.transform.childCount > 0)
            Add(messages,
                LiveMirroringValidationSeverity.Warning,
                "Setup Holder Contains Child Content",
                "Move creator content outside the setup holder when its hierarchy path must remain unchanged. Cleanup preserves child objects but moves them to the holder's parent.",
                "@setup");
        Transform root = LiveMirroringSetupUtility.ResolveAuthoringRoot(system);
        GameObject cleanupRoot = root != null
            ? root.gameObject
            : system.transform.root.gameObject;
        if (!CreatorBuildCleaner.CanRemoveAuthoringHolder(
                system, cleanupRoot, out string cleanupFailure, out _))
            Add(messages,
                LiveMirroringValidationSeverity.Error,
                system.gameObject == cleanupRoot
                    ? "Setup Holder Is Prefab Root"
                    : "Setup Holder Contains Unrelated Components",
                cleanupFailure,
                "@setup");
        if (!system.applyScaleReference)
            Error(messages,
                "Shared Scaling Is Required",
                "Enable Synchronize Scale so all constraint targets scale the prefab consistently.",
                "applyScaleReference");
        else if (system.scaleReference == null)
            Error(messages,
                "Prefab Scale Object Required",
                "Assign the prefab object or content container that scales with the targets.",
                "scaleReference");
        else if (!LiveMirroringSetupUtility.ValidateScaleReferenceForSystem(
                     system, out string scaleReferenceError))
            Error(messages,
                "Invalid Prefab Scale Object",
                scaleReferenceError,
                "scaleReference");
        if (system.addParentConstraintToPrefabContainer)
        {
            if (!VrcConstraintUtility.HasParentConstraint)
                Error(messages,
                    "VRC Parent Constraint Unavailable",
                    "The installed VRChat SDK does not provide VRC Parent Constraints. Update the SDK or disable Constrain Prefab to Targets.",
                    "addParentConstraintToPrefabContainer");
            int sourceCount = 0;
            if (system.pairs != null)
                for (int i = 0; i < system.pairs.Length; i++)
                {
                    AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
                    if (pair == null) continue;
                    sourceCount++;
                    if (system.ShouldCreateOppositeTarget(pair)) sourceCount++;
                }
            if (sourceCount > 16)
                Error(messages,
                    "Too Many Constraint Sources",
                    $"The Prefab Container would use {sourceCount} constraint sources, but VRChat supports 16. Reduce generated targets or disable Constrain Prefab to Targets.",
                    "addParentConstraintToPrefabContainer");
        }
        if (system.showScenePreview && system.previewSource == null)
            Add(messages,
                LiveMirroringValidationSeverity.Info,
                "Preview Object Not Assigned",
                "Assign a Preview Object to display Scene Preview. Mirroring still works without one.",
                "previewSource");
        if (system.pairs == null) {
            Add(messages,
                LiveMirroringValidationSeverity.Error,
                "Saved Target List Is Damaged",
                "The saved target list is missing. Restore an unaffected copy before continuing. An empty saved list remains supported.",
                "pairs");
            return;
        }
        if (system.pairs.Length == 0) {
            Add(messages,
                LiveMirroringValidationSeverity.Info,
                "No Targets",
                "Add a target before building this setup.",
                "pairs");
            return;
        }
        for (int i = 0; i < graph.PairFacts.Count; i++) {
            LiveMirroringPairFact fact = graph.PairFacts[i];
            AuthoringLiveMirroringSystem.MirrorPair pair = fact.Pair;
            if (fact.Status == LiveMirroringPairStatus.Accepted) {
                if (root != null && (!IsWithin(pair.sourceTarget, root) ||
                    !IsWithin(pair.mirroredTarget, root)))
                    Add(messages, LiveMirroringValidationSeverity.Warning,
                        PairName(pair, fact.Index),
                        "One or both targets are outside the Prefab Root and may be lost when the prefab is saved. Move them under the Prefab Root.",
                        PairPath(fact.Index));
                continue;
            }
            string issue = PairIssue(fact.Status);
            if (issue != null)
                Add(messages,
                    fact.Status == LiveMirroringPairStatus.MissingReference
                        ? LiveMirroringValidationSeverity.Info
                        : LiveMirroringValidationSeverity.Error,
                    PairName(pair, fact.Index), issue, PairPath(fact.Index));
        }
    }
    internal static string PairIssue(LiveMirroringPairStatus status) => status switch {
        LiveMirroringPairStatus.MissingPair => "Target data is missing.",
        LiveMirroringPairStatus.MissingReference => "ThreadLight creates a missing source or mirrored target during Build.",
        LiveMirroringPairStatus.SameObject => "Assign different source and mirrored targets.",
        LiveMirroringPairStatus.NestedTargets => "Assign separate targets; a parent and child cannot mirror each other.",
        LiveMirroringPairStatus.PersistentReference => "Choose scene objects instead of prefab-asset references.",
        LiveMirroringPairStatus.CrossSceneReference => "Move both targets into the setup's scene.",
        LiveMirroringPairStatus.DuplicateTarget => "Another enabled pair already controls this mirrored target.",
        LiveMirroringPairStatus.Cycle => "This relationship creates a mirroring cycle. Change or remove one relationship.",
        _ => null
    };
    private static void Error(
        List<LiveMirroringValidationMessage> messages,
        string title,
        string message,
        string propertyPath) => Add(messages,
            LiveMirroringValidationSeverity.Error, title, message, propertyPath);
    private static bool HasSupportedDataVersion(
        AuthoringLiveMirroringSystem system,
        List<LiveMirroringValidationMessage> messages) {
        if (system.DataVersion < 0) {
            Error(messages,
                "Saved Setup Is Damaged",
                "This Live Mirroring setup has an invalid saved version. Restore an unaffected copy before continuing. No changes were made.",
                "@setup");
            return false;
        }
        if (system.DataVersion >
            LiveMirroringMigrationService.CurrentDataVersion) {
            Error(messages,
                "Newer ThreadLight Authoring Required",
                "This setup was saved by a newer ThreadLight Authoring version. Update ThreadLight Authoring, then reopen it. No changes were made.",
                "@setup");
            return false;
        }
        return true;
    }
    private static void Add(
        List<LiveMirroringValidationMessage> messages,
        LiveMirroringValidationSeverity severity,
        string title,
        string message,
        string propertyPath) => messages.Add(
            new LiveMirroringValidationMessage(
                severity, title, message, propertyPath));
    private static string PairPath(int index) =>
        $"pairs.Array.data[{index}]";
    private static string PairName(
        AuthoringLiveMirroringSystem.MirrorPair pair,
        int index) {
        return pair != null && !string.IsNullOrWhiteSpace(pair.pairName)
            ? pair.pairName.Trim()
            : $"Target {index + 1}";
    }
    private static bool IsWithin(Transform target, Transform root) {
        return target != null &&
               (target == root || target.IsChildOf(root));
    }
}
}
