namespace Threadlight.Mirroring.Editor {
using System;
using System.Collections.Generic;
using UnityEngine;
public sealed partial class LiveMirroringSetupWindow {
    private WorkspaceCompositionSnapshot renderedWorkspaceComposition;

    private void RememberWorkspaceComposition() {
        bool managed = IsCurrentSystemManagedExternally(
            out string managerName);
        RememberWorkspaceComposition(managed, managerName);
    }

    private void RememberWorkspaceComposition(
        bool managed,
        string managerName) {
        renderedWorkspaceComposition = WorkspaceCompositionSnapshot.Capture(
            currentSystem, managed, managerName);
    }

    private bool WorkspaceCompositionChanged() {
        WorkspaceCompositionSnapshot current =
            WorkspaceCompositionSnapshot.Capture(
                currentSystem,
                IsCurrentSystemManagedExternally(out string managerName),
                managerName);
        if (renderedWorkspaceComposition != null &&
            renderedWorkspaceComposition.PairTopologyChanged(current))
        {
            ResetTargetCardExpansionForExternalTopologyChange(
                Math.Max(renderedWorkspaceComposition.PairCount,
                    current?.PairCount ?? 0));
        }
        return renderedWorkspaceComposition == null ||
            renderedWorkspaceComposition.RequiresRebuild(current);
    }

    private void RefreshRetainedWorkspace() {
        if (currentSystem == null ||
            serializedSystem == null ||
            serializedSystem.targetObject != currentSystem) {
            RebuildWorkspace();
            return;
        }
        serializedSystem.UpdateIfRequiredOrScript();
        RememberWorkspaceComposition();
        RebuildValidation();
        RefreshAddSelectedObjectsState();
        workspace?.MarkDirtyRepaint();
    }

    private sealed class WorkspaceCompositionSnapshot {
        private readonly int mode;
        private readonly string managerName;
        private readonly bool addVrcfuryArmatureLinks;
        private readonly bool generateBootstrapper;
        private readonly PairSnapshot[] pairs;
        private readonly int[] scaleHandles;
        public int PairCount => pairs.Length;

        private WorkspaceCompositionSnapshot(
            int mode,
            string managerName,
            bool addVrcfuryArmatureLinks,
            bool generateBootstrapper,
            PairSnapshot[] pairs,
            int[] scaleHandles) {
            this.mode = mode;
            this.managerName = managerName ?? string.Empty;
            this.addVrcfuryArmatureLinks = addVrcfuryArmatureLinks;
            this.generateBootstrapper = generateBootstrapper;
            this.pairs = pairs ?? Array.Empty<PairSnapshot>();
            this.scaleHandles = scaleHandles ?? Array.Empty<int>();
        }

        public static WorkspaceCompositionSnapshot Capture(
            AuthoringLiveMirroringSystem system,
            bool managed,
            string managerName) {
            if (system == null)
                return new WorkspaceCompositionSnapshot(
                    0, null, false, false, null, null);
            int mode = !SupportsInstalledData(system)
                ? system.DataVersion > LiveMirroringMigrationService.CurrentDataVersion
                    ? 1
                    : 2
                : managed ? 3 : 4;
            if (mode != 4)
                return new WorkspaceCompositionSnapshot(
                    mode,
                    managed ? managerName : null,
                    false,
                    false,
                    null,
                    null);
            AuthoringLiveMirroringSystem.MirrorPair[] sourcePairs = system.pairs;
            PairSnapshot[] pairs = new PairSnapshot[sourcePairs?.Length ?? 0];
            for (int index = 0; index < pairs.Length; index++)
                pairs[index] = new PairSnapshot(sourcePairs[index]);
            Transform[] sourceHandles = system.scaleHandles;
            int[] handles = new int[sourceHandles?.Length ?? 0];
            for (int index = 0; index < handles.Length; index++)
                handles[index] = sourceHandles[index] != null
                    ? sourceHandles[index].GetInstanceID()
                    : 0;
            return new WorkspaceCompositionSnapshot(
                mode,
                managed ? managerName : null,
                system.addVrcfuryArmatureLinks,
                system.generateThreadlightComponentsBootstrapper,
                pairs,
                handles);
        }

        public bool RequiresRebuild(WorkspaceCompositionSnapshot current) {
            if (current == null ||
                mode != current.mode ||
                !string.Equals(managerName, current.managerName,
                    StringComparison.Ordinal) ||
                addVrcfuryArmatureLinks != current.addVrcfuryArmatureLinks ||
                generateBootstrapper != current.generateBootstrapper ||
                pairs.Length != current.pairs.Length ||
                scaleHandles.Length != current.scaleHandles.Length)
                return true;
            for (int index = 0; index < pairs.Length; index++)
                if (pairs[index].CreateOppositeTarget !=
                        current.pairs[index].CreateOppositeTarget ||
                    pairs[index].UseGlobalSideLabels !=
                        current.pairs[index].UseGlobalSideLabels)
                    return true;
            return PairTopologyChanged(current) ||
                SequenceReordered(scaleHandles, current.scaleHandles);
        }

        public bool PairTopologyChanged(WorkspaceCompositionSnapshot current) {
            if (current == null || pairs.Length != current.pairs.Length)
                return true;
            if (SequenceReordered(pairs, current.pairs))
                return true;
            int changedSlots = 0;
            for (int index = 0; index < pairs.Length; index++)
                if (!pairs[index].Equals(current.pairs[index]) &&
                    ++changedSlots > 1)
                    return true;
            return false;
        }

        private static bool SequenceReordered<T>(T[] previous, T[] current) {
            bool sequenceEqual = true;
            for (int index = 0; index < previous.Length; index++)
                if (!EqualityComparer<T>.Default.Equals(
                        previous[index], current[index])) {
                    sequenceEqual = false;
                    break;
                }
            if (sequenceEqual)
                return false;
            Dictionary<T, int> counts = new Dictionary<T, int>();
            for (int index = 0; index < previous.Length; index++) {
                counts.TryGetValue(previous[index], out int count);
                counts[previous[index]] = count + 1;
            }
            for (int index = 0; index < current.Length; index++) {
                if (!counts.TryGetValue(current[index], out int count))
                    return false;
                if (count == 1)
                    counts.Remove(current[index]);
                else
                    counts[current[index]] = count - 1;
            }
            return counts.Count == 0;
        }
    }

    private readonly struct PairSnapshot : IEquatable<PairSnapshot> {
        public readonly bool CreateOppositeTarget;
        public readonly bool UseGlobalSideLabels;
        private readonly bool mirrorEnabled;
        private readonly string pairName;
        private readonly string sourceSideLabel;
        private readonly string mirroredSideLabel;
        private readonly int sourceBone;
        private readonly int mirroredBone;
        private readonly int sourceTarget;
        private readonly int mirroredTarget;
        private readonly Vector3 mirroredRotationOffset;

        public PairSnapshot(AuthoringLiveMirroringSystem.MirrorPair pair) {
            CreateOppositeTarget = pair?.createOppositeTarget ?? false;
            UseGlobalSideLabels = pair?.useGlobalSideLabels ?? false;
            mirrorEnabled = pair?.mirrorEnabled ?? false;
            pairName = pair?.pairName ?? string.Empty;
            sourceSideLabel = pair?.sourceSideLabel ?? string.Empty;
            mirroredSideLabel = pair?.mirroredSideLabel ?? string.Empty;
            sourceBone = pair != null ? (int)pair.sourceBone : -1;
            mirroredBone = pair != null ? (int)pair.mirroredBone : -1;
            sourceTarget = pair?.sourceTarget != null
                ? pair.sourceTarget.GetInstanceID()
                : 0;
            mirroredTarget = pair?.mirroredTarget != null
                ? pair.mirroredTarget.GetInstanceID()
                : 0;
            mirroredRotationOffset = pair?.mirroredRotationOffset ?? default;
        }

        public bool Equals(PairSnapshot other) =>
            CreateOppositeTarget == other.CreateOppositeTarget &&
            UseGlobalSideLabels == other.UseGlobalSideLabels &&
            mirrorEnabled == other.mirrorEnabled &&
            string.Equals(pairName, other.pairName, StringComparison.Ordinal) &&
            string.Equals(sourceSideLabel, other.sourceSideLabel,
                StringComparison.Ordinal) &&
            string.Equals(mirroredSideLabel, other.mirroredSideLabel,
                StringComparison.Ordinal) &&
            sourceBone == other.sourceBone &&
            mirroredBone == other.mirroredBone &&
            sourceTarget == other.sourceTarget &&
            mirroredTarget == other.mirroredTarget &&
            mirroredRotationOffset.Equals(other.mirroredRotationOffset);

        public override bool Equals(object obj) =>
            obj is PairSnapshot other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                int hash = CreateOppositeTarget ? 1 : 0;
                hash = hash * 31 + (UseGlobalSideLabels ? 1 : 0);
                hash = hash * 31 + (mirrorEnabled ? 1 : 0);
                hash = hash * 31 + pairName.GetHashCode();
                hash = hash * 31 + sourceSideLabel.GetHashCode();
                hash = hash * 31 + mirroredSideLabel.GetHashCode();
                hash = hash * 31 + sourceBone;
                hash = hash * 31 + mirroredBone;
                hash = hash * 31 + sourceTarget;
                hash = hash * 31 + mirroredTarget;
                hash = hash * 31 + mirroredRotationOffset.GetHashCode();
                return hash;
            }
        }
    }
}
}
