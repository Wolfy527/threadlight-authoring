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
        Collect(system, messages, componentBuffer);
        LiveMirroringEditorExtensionRegistry.DispatchOptionalIsolated(
            LiveMirroringEditorExtensionRegistry.GetValidators(),
            failedValidators ?? new HashSet<string>(),
            LiveMirroringExtensionCapabilities.Validation,
            system,
            contributor => contributor.Validate(serializedSystem, messages));
    }
    internal static void Collect(
        AuthoringLiveMirroringSystem system,
        List<LiveMirroringValidationMessage> messages,
        List<Component> componentBuffer) {
        if (system == null || messages == null)
            return;
        LiveMirroringEvaluationBuffers graph = LiveMirroringService.AnalyzePairs(system);
        if (!system.gameObject.CompareTag("EditorOnly"))
            Add(messages,
                LiveMirroringValidationSeverity.Warning,
                "Setup Holder Is Not EditorOnly",
                "Live Mirroring removes its holder during play mode and upload. Keep it on a dedicated EditorOnly object; child objects are preserved and moved to the holder's parent.",
                "@setup");
        if (system.transform.childCount > 0)
            Add(messages,
                LiveMirroringValidationSeverity.Warning,
                "Setup Holder Contains Child Content",
                "Child objects are preserved during cleanup, but move to the holder's parent. Keep creator content outside the Live Mirroring holder when its hierarchy path must remain unchanged.",
                "@setup");
        if (ContainsUnrelatedComponent(system, componentBuffer))
            Add(messages,
                LiveMirroringValidationSeverity.Error,
                "Setup Holder Contains Unrelated Components",
                "Components on the Live Mirroring holder are removed with it during play mode and upload. Move unrelated components onto another object before continuing.",
                "@setup");
        Transform root = LiveMirroringSetupUtility.ResolveAuthoringRoot(system);
        if (!system.applyScaleReference)
            Error(messages,
                "Shared Scaling Is Required",
                "Enable Synchronize Scale so every constraint target scales the prefab consistently.",
                "applyScaleReference");
        else if (system.scaleReference == null)
            Error(messages,
                "Prefab Scale Reference Required",
                "Assign the prefab object or content container that should scale with the constraint targets.",
                "scaleReference");
        else if (!LiveMirroringSetupUtility.ValidateScaleReferenceForSystem(
                     system, out string scaleReferenceError))
            Error(messages,
                "Invalid Prefab Scale Reference",
                scaleReferenceError,
                "scaleReference");
        if (system.addParentConstraintToPrefabContainer)
        {
            if (!VrcConstraintUtility.HasParentConstraint)
                Error(messages,
                    "VRC Parent Constraint Unavailable",
                    "The installed VRChat SDK does not provide the Parent Constraint requested for the prefab container.",
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
                    $"The Prefab Container would have {sourceCount} sources, but VRChat supports 16 keyable sources.",
                    "addParentConstraintToPrefabContainer");
        }
        if (system.showScenePreview && system.previewSource == null)
            Add(messages,
                LiveMirroringValidationSeverity.Info,
                "Preview Source Not Assigned",
                "Assign a preview source to display scene ghosts. Mirroring still works without one.",
                "previewSource");
        if (system.pairs == null || system.pairs.Length == 0) {
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
                        "One or both references are outside the configured prefab root and may not survive prefab saving.",
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
    private static string PairIssue(LiveMirroringPairStatus status) => status switch {
        LiveMirroringPairStatus.MissingPair => "The pair data is missing.",
        LiveMirroringPairStatus.MissingReference => "A missing source or mirrored reference will receive a generated target when you Build.",
        LiveMirroringPairStatus.SelfReference => "The source and mirrored target must be different objects.",
        LiveMirroringPairStatus.DuplicateTarget => "Another enabled pair already controls this mirrored target.",
        LiveMirroringPairStatus.Cycle => "This pair creates a mirroring cycle and will be skipped.",
        _ => null
    };
    private static void Error(
        List<LiveMirroringValidationMessage> messages,
        string title,
        string message,
        string propertyPath) => Add(messages,
            LiveMirroringValidationSeverity.Error, title, message, propertyPath);
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
    private static bool ContainsUnrelatedComponent(
        AuthoringLiveMirroringSystem system,
        List<Component> componentBuffer) {
        if (system == null)
            return false;
        if (componentBuffer == null) {
            Component[] components = system.GetComponents<Component>();
            return ContainsNonAuthoringComponent(system, components);
        }
        componentBuffer.Clear();
        system.GetComponents(componentBuffer);
        return ContainsNonAuthoringComponent(system, componentBuffer);
    }
    private static bool ContainsNonAuthoringComponent(
        AuthoringLiveMirroringSystem system,
        IReadOnlyList<Component> components) {
        for (int i = 0; i < components.Count; i++) {
            Component component = components[i];
            if (component == null ||
                component is Transform ||
                component == system ||
                component is CreatorHierarchyMetadata ||
                component is CreatorGeneratedEditorOnlyObject) {
                continue;
            }
            return true;
        }
        return false;
    }
}
}
