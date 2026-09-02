#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using System.Collections.Generic;
using Threadlight.Authoring;
using UnityEngine;

internal enum LiveMirroringPairStatus
{
    Accepted, Disabled, MissingPair, MissingReference, SelfReference, DuplicateTarget, Cycle
}

internal readonly struct LiveMirroringPairFact
{
    public readonly int Index;
    public readonly AuthoringLiveMirroringSystem.MirrorPair Pair;
    public readonly LiveMirroringPairStatus Status;
    public LiveMirroringPairFact(int index, AuthoringLiveMirroringSystem.MirrorPair pair, LiveMirroringPairStatus status)
    {
        Index = index; Pair = pair; Status = status;
    }
}

/// <summary>Cached topology and derived facts consumed by mirroring, validation, preview, and builders.</summary>
internal sealed class LiveMirroringEvaluationBuffers
{
    private static int hierarchyRevision;

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterHierarchyInvalidation()
    {
        UnityEditor.EditorApplication.hierarchyChanged -= InvalidateHierarchy;
        UnityEditor.EditorApplication.hierarchyChanged += InvalidateHierarchy;
    }

    private static void InvalidateHierarchy() => hierarchyRevision++;

    private readonly struct TransformSlot
    {
        private readonly Transform value;
        private readonly bool alive;
        public TransformSlot(Transform value) { this.value = value; alive = value != null; }
        public bool Matches(Transform candidate) => ReferenceEquals(value, candidate) && alive == (candidate != null);
    }

    private readonly struct PairSlot
    {
        private readonly AuthoringLiveMirroringSystem.MirrorPair pair;
        private readonly bool enabled, createOpposite;
        private readonly TransformSlot source, target;
        public PairSlot(AuthoringLiveMirroringSystem.MirrorPair pair)
        {
            this.pair = pair; enabled = pair?.mirrorEnabled ?? false;
            createOpposite = pair?.createOppositeTarget ?? false;
            source = new TransformSlot(pair?.sourceTarget);
            target = new TransformSlot(pair?.mirroredTarget);
        }
        public bool Matches(AuthoringLiveMirroringSystem.MirrorPair candidate) => ReferenceEquals(pair, candidate) &&
            (candidate == null || enabled == candidate.mirrorEnabled &&
             createOpposite == candidate.createOppositeTarget &&
             source.Matches(candidate.sourceTarget) &&
             target.Matches(candidate.mirroredTarget));
    }

    public readonly List<Transform> Targets = new List<Transform>();
    public readonly HashSet<Transform> TargetSet = new HashSet<Transform>();
    public readonly List<LiveMirroringPairFact> PairFacts = new List<LiveMirroringPairFact>();
    public readonly List<AuthoringLiveMirroringSystem.MirrorPair> EnabledPairs = new List<AuthoringLiveMirroringSystem.MirrorPair>();
    internal readonly HashSet<Transform> ActiveTargets = new HashSet<Transform>();
    private readonly List<PairSlot> pairSlots = new List<PairSlot>();
    private readonly List<TransformSlot> handleSlots = new List<TransformSlot>();
    private readonly HashSet<Transform> controlled = new HashSet<Transform>();
    private readonly Dictionary<Transform, List<Transform>> edges = new Dictionary<Transform, List<Transform>>();
    private readonly Stack<List<Transform>> edgeListPool = new Stack<List<Transform>>();
    private readonly HashSet<Transform> visited = new HashSet<Transform>();
    private readonly Stack<Transform> pending = new Stack<Transform>();
    private TransformSlot scaleReferenceSlot, rootSlot;
    private bool initialized, pairsNull, handlesNull;
    private int capturedHierarchyRevision;
    internal bool ScaleTopologyValid { get; private set; }
    internal int ScaleHandleSignature { get; private set; }

    public void Analyze(AuthoringLiveMirroringSystem system)
    {
        AuthoringLiveMirroringSystem.MirrorPair[] pairs = system?.pairs;
        Transform[] handles = system?.scaleHandles;
        if (Matches(system, pairs, handles)) return;
        Capture(system, pairs, handles);
        Targets.Clear(); TargetSet.Clear(); PairFacts.Clear(); EnabledPairs.Clear(); ActiveTargets.Clear();
        controlled.Clear(); RecycleEdges();
        if (handles != null) for (int i = 0; i < handles.Length; i++) AddTarget(handles[i]);
        if (pairs != null) for (int i = 0; i < pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = pairs[i];
            AddTarget(pair?.sourceTarget);
            if (system.ShouldCreateOppositeTarget(pair))
                AddTarget(pair?.mirroredTarget);
            if (pair?.sourceTarget != null) ActiveTargets.Add(pair.sourceTarget);
            if (system.ShouldCreateOppositeTarget(pair) &&
                pair.mirroredTarget != null) ActiveTargets.Add(pair.mirroredTarget);
            LiveMirroringPairStatus status = Classify(system, pair);
            PairFacts.Add(new LiveMirroringPairFact(i, pair, status));
            if (status == LiveMirroringPairStatus.Accepted) EnabledPairs.Add(pair);
        }
        CaptureScaleTopology(system);
    }

    private bool Matches(AuthoringLiveMirroringSystem system,
        AuthoringLiveMirroringSystem.MirrorPair[] pairs, Transform[] handles)
    {
        if (!initialized || Targets.Count != TargetSet.Count ||
            capturedHierarchyRevision != hierarchyRevision ||
            !scaleReferenceSlot.Matches(system?.scaleReference) ||
            !rootSlot.Matches(LiveMirroringService.ResolveRoot(system)) ||
            pairsNull != (pairs == null) || handlesNull != (handles == null) ||
            pairSlots.Count != (pairs?.Length ?? 0) || handleSlots.Count != (handles?.Length ?? 0)) return false;
        for (int i = 0; i < pairSlots.Count; i++) if (!pairSlots[i].Matches(pairs[i])) return false;
        for (int i = 0; i < handleSlots.Count; i++) if (!handleSlots[i].Matches(handles[i])) return false;
        return true;
    }

    private void Capture(AuthoringLiveMirroringSystem system,
        AuthoringLiveMirroringSystem.MirrorPair[] pairs, Transform[] handles)
    {
        initialized = true; pairsNull = pairs == null; handlesNull = handles == null;
        capturedHierarchyRevision = hierarchyRevision;
        scaleReferenceSlot = new TransformSlot(system?.scaleReference);
        rootSlot = new TransformSlot(LiveMirroringService.ResolveRoot(system));
        pairSlots.Clear(); handleSlots.Clear();
        if (pairs != null) for (int i = 0; i < pairs.Length; i++) pairSlots.Add(new PairSlot(pairs[i]));
        if (handles != null) for (int i = 0; i < handles.Length; i++) handleSlots.Add(new TransformSlot(handles[i]));
    }

    private void AddTarget(Transform target)
    {
        if (target != null && TargetSet.Add(target)) Targets.Add(target);
    }

    private void CaptureScaleTopology(AuthoringLiveMirroringSystem system)
    {
        ScaleTopologyValid = LiveMirroringService.TryCaptureScaleTopology(
            system, Targets, out int signature);
        ScaleHandleSignature = signature;
    }

    private LiveMirroringPairStatus Classify(
        AuthoringLiveMirroringSystem system,
        AuthoringLiveMirroringSystem.MirrorPair pair)
    {
        if (pair == null) return LiveMirroringPairStatus.MissingPair;
        if (!system.ShouldMirrorOppositeTarget(pair))
            return LiveMirroringPairStatus.Disabled;
        if (pair.sourceTarget == null || pair.mirroredTarget == null) return LiveMirroringPairStatus.MissingReference;
        if (pair.sourceTarget == pair.mirroredTarget) return LiveMirroringPairStatus.SelfReference;
        if (!controlled.Add(pair.mirroredTarget)) return LiveMirroringPairStatus.DuplicateTarget;
        if (CanReach(pair.mirroredTarget, pair.sourceTarget))
        {
            controlled.Remove(pair.mirroredTarget);
            return LiveMirroringPairStatus.Cycle;
        }
        if (!edges.TryGetValue(pair.sourceTarget, out List<Transform> next))
        {
            next = edgeListPool.Count > 0
                ? edgeListPool.Pop()
                : new List<Transform>();
            edges.Add(pair.sourceTarget, next);
        }
        next.Add(pair.mirroredTarget);
        return LiveMirroringPairStatus.Accepted;
    }

    private void RecycleEdges()
    {
        foreach (List<Transform> next in edges.Values)
        {
            next.Clear();
            edgeListPool.Push(next);
        }
        edges.Clear();
    }

    private bool CanReach(Transform start, Transform target)
    {
        visited.Clear(); pending.Clear(); pending.Push(start);
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null || !visited.Add(current)) continue;
            if (current == target) return true;
            if (!edges.TryGetValue(current, out List<Transform> next)) continue;
            for (int i = 0; i < next.Count; i++) pending.Push(next[i]);
        }
        return false;
    }
}

public static class LiveMirroringService
{
    private readonly struct ScaleObservation
    {
        internal readonly Vector3 DesiredScale;
        internal readonly bool WritesNeeded;
        internal ScaleObservation(Vector3 desiredScale, bool writesNeeded)
        {
            DesiredScale = desiredScale;
            WritesNeeded = writesNeeded;
        }
    }

    public static void UpdateMirroring(AuthoringLiveMirroringSystem system)
    {
        LiveMirroringProcessorRegistry.Run(system, LiveMirroringProcessingStage.BeforeCore);
        LiveMirroringEvaluationBuffers evaluation = Evaluate(system);
        ApplyScaleReference(system, evaluation);
        Mirror(system, evaluation.EnabledPairs, ResolveRoot(system));
        LiveMirroringProcessorRegistry.Run(system, LiveMirroringProcessingStage.AfterCore);
    }

    public static void ApplyScaleReference(AuthoringLiveMirroringSystem system)
    {
        if (system != null) ApplyScaleReference(system, Evaluate(system));
    }

    public static void ApplyScaleReference(AuthoringLiveMirroringSystem system, IReadOnlyList<Transform> targets)
    {
        if (system == null || !system.applyScaleReference || system.scaleReference == null || targets == null ||
            !TryCaptureScaleTopology(system, targets, out int signature)) return;
        ApplyScaleReference(system, targets, signature);
    }

    internal static void ApplyScaleReference(
        AuthoringLiveMirroringSystem system, LiveMirroringEvaluationBuffers evaluation)
    {
        if (system == null || !system.applyScaleReference ||
            system.scaleReference == null || !evaluation.ScaleTopologyValid) return;
        ApplyScaleReference(system, evaluation.Targets,
            evaluation.ScaleHandleSignature);
    }

    private static void ApplyScaleReference(AuthoringLiveMirroringSystem system,
        IReadOnlyList<Transform> targets, int signature)
    {
        Vector3 referenceScale = system.scaleReference.lossyScale;
        bool topologyChanged = !system.scaleSynchronizationInitialized ||
            system.synchronizedScaleReference != system.scaleReference || system.synchronizedHandleSignature != signature;
        ScaleObservation observation = ObserveScale(targets, referenceScale,
            system.synchronizedWorldScale, topologyChanged,
            system.preferScaleReferenceAfterUndo);
        system.preferScaleReferenceAfterUndo = false;
        int undo = !topologyChanged && observation.WritesNeeded
            ? RecordScaleUndo(system, targets) : -1;
        if (observation.WritesNeeded)
        {
            ApplyWorldScale(system.scaleReference, observation.DesiredScale);
            for (int i = 0; i < targets.Count; i++)
                ApplyWorldScale(targets[i], observation.DesiredScale);
        }
        if (undo >= 0) UnityEditor.Undo.CollapseUndoOperations(undo);
        system.scaleSynchronizationInitialized = true;
        system.synchronizedWorldScale = observation.DesiredScale;
        system.synchronizedScaleReference = system.scaleReference;
        system.synchronizedHandleSignature = signature;
    }

    public static void MirrorEnabledPairs(AuthoringLiveMirroringSystem system)
    {
        if (system == null) return;
        LiveMirroringEvaluationBuffers evaluation = Evaluate(system);
        Mirror(system, evaluation.EnabledPairs, ResolveRoot(system));
    }

    public static List<Transform> CollectAllTargets(AuthoringLiveMirroringSystem system) =>
        new List<Transform>(system == null ? System.Array.Empty<Transform>() : Evaluate(system).Targets);

    public static void CollectAllTargets(AuthoringLiveMirroringSystem system, List<Transform> output, HashSet<Transform> seen)
    {
        if (output == null || seen == null) return;
        output.Clear(); seen.Clear();
        if (system == null) return;
        IReadOnlyList<Transform> targets = Evaluate(system).Targets;
        for (int i = 0; i < targets.Count; i++) if (targets[i] != null && seen.Add(targets[i])) output.Add(targets[i]);
    }

    public static Transform ResolveRoot(AuthoringLiveMirroringSystem system) => system == null ? null :
        system.mirrorCenter != null ? system.mirrorCenter :
        system.transform.parent != null ? system.transform.parent : system.transform.root;

    /// <summary>
    /// Resolves the prefab root for both the legacy sibling-holder layout and
    /// the current holder-inside-target-root layout without relying on names.
    /// </summary>
    public static Transform ResolveAuthoringRoot(AuthoringLiveMirroringSystem system)
    {
        if (system == null) return null;
        Transform parent = system.transform.parent;
        if (parent == null) return ResolveRoot(system);
        CreatorHierarchyMetadata marker =
            parent.GetComponent<CreatorHierarchyMetadata>();
        return marker != null && marker.Matches(
            AuthoringLiveMirroringSystem.StandaloneOwnerId,
            AuthoringLiveMirroringSystem.StandaloneModuleId,
            AuthoringLiveMirroringSystem.TargetsRootStableId)
                ? parent.parent
                : ResolveNestedAuthoringRoot(system, parent);
    }

    private static Transform ResolveNestedAuthoringRoot(
        AuthoringLiveMirroringSystem system,
        Transform parent)
    {
        // A managing Builder replaces standalone ownership metadata when it
        // adopts the setup. The mirror center remains the package-independent
        // root contract, so a holder nested beneath it must still resolve to
        // that root rather than to its immediate target-folder parent.
        Transform center = system.mirrorCenter;
        return center != null && center != system.transform &&
               system.transform.IsChildOf(center)
            ? center
            : parent;
    }

    internal static LiveMirroringEvaluationBuffers AnalyzePairs(AuthoringLiveMirroringSystem system) => Evaluate(system);
    internal static LiveMirroringEvaluationBuffers Evaluate(AuthoringLiveMirroringSystem system)
    {
        LiveMirroringEvaluationBuffers evaluation = system.evaluationBuffers ??= new LiveMirroringEvaluationBuffers();
        evaluation.Analyze(system);
        return evaluation;
    }

    private static void Mirror(AuthoringLiveMirroringSystem system, IReadOnlyList<AuthoringLiveMirroringSystem.MirrorPair> pairs,
        Transform center)
    {
        if (center == null) return;
        system.mirrorOptions ??= new AuthoringLiveMirroringSystem.MirrorOptions();
        for (int i = 0; i < pairs.Count; i++) MirrorPair(system, pairs[i], center);
    }

    private static void MirrorPair(AuthoringLiveMirroringSystem system, AuthoringLiveMirroringSystem.MirrorPair pair, Transform center)
    {
        Transform source = pair.sourceTarget, target = pair.mirroredTarget;
        if (system.mirrorOptions.mirrorPosition)
        {
            Vector3 position = center.TransformPoint(MirrorVector(system, center.InverseTransformPoint(source.position)));
            if ((target.position - position).sqrMagnitude > .00000001f) target.position = position;
        }
        if (system.mirrorOptions.mirrorRotation)
        {
            Vector3 forward = center.TransformDirection(MirrorVector(system, center.InverseTransformDirection(source.forward)));
            Vector3 up = center.TransformDirection(MirrorVector(system, center.InverseTransformDirection(source.up)));
            if (forward.sqrMagnitude >= .0001f && up.sqrMagnitude >= .0001f)
            {
                Quaternion rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(pair.mirroredRotationOffset);
                if (Quaternion.Angle(target.rotation, rotation) > .001f) target.rotation = rotation;
            }
        }
        if (system.mirrorOptions.mirrorScale) ApplyWorldScale(target, source.lossyScale);
    }

    private static Vector3 MirrorVector(AuthoringLiveMirroringSystem system, Vector3 value)
    {
        if (system.mirrorOptions.mirrorAxis == AuthoringLiveMirroringSystem.Axis.X) value.x *= -1;
        else if (system.mirrorOptions.mirrorAxis == AuthoringLiveMirroringSystem.Axis.Y) value.y *= -1;
        else value.z *= -1;
        return value;
    }

    private static void ApplyWorldScale(Transform target, Vector3 worldScale)
    {
        if (target == null) return;
        Vector3 scale = worldScale;
        if (target.parent != null)
        {
            Vector3 parent = target.parent.lossyScale;
            scale = new Vector3(Divide(worldScale.x, parent.x), Divide(worldScale.y, parent.y), Divide(worldScale.z, parent.z));
        }
        if ((target.localScale - scale).sqrMagnitude > .00000001f) target.localScale = scale;
    }

    private static ScaleObservation ObserveScale(
        IReadOnlyList<Transform> targets,
        Vector3 referenceScale,
        Vector3 previousScale,
        bool topologyChanged,
        bool preferReference)
    {
        bool referenceChanged = !Approximately(referenceScale, previousScale);
        bool allMatchReference = true;
        bool allMatchEdited = true;
        bool sawUneditedTarget = false;
        bool foundEditedTarget = false;
        Vector3 editedScale = previousScale;
        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null) continue;
            Vector3 scale = target.lossyScale;
            if (!Approximately(scale, referenceScale))
                allMatchReference = false;
            if (!foundEditedTarget)
            {
                if (Approximately(scale, previousScale))
                    sawUneditedTarget = true;
                else
                {
                    foundEditedTarget = true;
                    editedScale = scale;
                    allMatchEdited = !sawUneditedTarget;
                }
            }
            else if (!Approximately(scale, editedScale))
                allMatchEdited = false;
        }
        Vector3 desired = referenceScale;
        if (!topologyChanged && !preferReference)
        {
            if (foundEditedTarget)
                desired = referenceChanged && allMatchReference
                    ? referenceScale : editedScale;
            else if (!referenceChanged)
                desired = previousScale;
        }
        bool writesNeeded = Approximately(desired, referenceScale)
            ? !allMatchReference
            : !allMatchEdited || !Approximately(referenceScale, desired);
        return new ScaleObservation(desired, writesNeeded);
    }

    private static int RecordScaleUndo(
        AuthoringLiveMirroringSystem system, IReadOnlyList<Transform> targets)
    {
        if (system.undoRefreshQueued || UnityEditor.EditorUtility.IsPersistent(system)) return -1;
        List<Object> objects = new List<Object> { system.scaleReference };
        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null && targets[i] != system.scaleReference) objects.Add(targets[i]);
        int group = UnityEditor.Undo.GetCurrentGroup();
        UnityEditor.Undo.SetCurrentGroupName("Scale Live Mirroring Setup");
        UnityEditor.Undo.RecordObjects(objects.ToArray(), "Scale Live Mirroring Setup");
        return group;
    }

    public static bool HasValidScaleReferenceTopology(AuthoringLiveMirroringSystem system, IReadOnlyList<Transform> targets)
        => TryCaptureScaleTopology(system, targets, out _);

    internal static bool TryCaptureScaleTopology(AuthoringLiveMirroringSystem system,
        IReadOnlyList<Transform> targets, out int signature)
    {
        signature = 17;
        if (system == null || system.scaleReference == null || targets == null)
            return false;
        Transform reference = system.scaleReference;
        bool valid = reference != system.transform && reference != ResolveRoot(system);
        unchecked
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                signature = signature * 31 +
                    (target != null ? target.GetInstanceID() : 0);
                if (target == reference || target != null &&
                    (target.IsChildOf(reference) || reference.IsChildOf(target)))
                    valid = false;
            }
        }
        return valid;
    }

    internal static bool HasValidScaleReferenceTopology(AuthoringLiveMirroringSystem system) =>
        system != null && Evaluate(system).ScaleTopologyValid;

    private static float Divide(float value, float divisor) => Mathf.Abs(divisor) < .00001f ? value : value / divisor;
    private static bool Approximately(Vector3 left, Vector3 right) => (left - right).sqrMagnitude <= .00000001f;
}
}
#endif
