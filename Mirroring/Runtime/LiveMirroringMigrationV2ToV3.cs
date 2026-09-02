#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using Threadlight.Authoring;

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Replaces unsafe prefab-root references with a uniquely owned generated
/// content container when one can be proven. Otherwise the reference is
/// cleared so shared scaling cannot modify unrelated creator content.
/// </summary>
internal sealed class LiveMirroringMigrationV2ToV3 :
    IVersionedDataMigration<AuthoringLiveMirroringSystem>
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public void Apply(AuthoringLiveMirroringSystem system)
    {
        if (system == null || system.scaleReference == null)
            return;

        List<Transform> targets =
            LiveMirroringService.CollectAllTargets(system);
        if (!LiveMirroringService.HasValidScaleReferenceTopology(
                system,
                targets))
        {
            system.scaleReference = FindGeneratedContentContainer(
                system,
                targets,
                out bool ambiguous);
            if (ambiguous)
            {
                Debug.LogWarning(
                    $"Live Mirroring found multiple compatible owned prefab " +
                    $"containers for '{system.name}'. Its Scale Reference was " +
                    $"cleared for safety; assign the intended container before " +
                    $"using shared scaling.",
                    system);
            }
        }
    }

    private static Transform FindGeneratedContentContainer(
        AuthoringLiveMirroringSystem system,
        IReadOnlyList<Transform> targets,
        out bool ambiguous)
    {
        ambiguous = false;
        Transform root = LiveMirroringService.ResolveAuthoringRoot(system);
        if (root == null)
            return null;

        CreatorHierarchyMetadata[] metadata =
            root.GetComponentsInChildren<CreatorHierarchyMetadata>(true);
        Transform match = null;

        for (int i = 0; i < metadata.Length; i++)
        {
            CreatorHierarchyMetadata candidate = metadata[i];
            if (candidate == null || !candidate.CreatedByBuilder ||
                !candidate.Matches(
                    AuthoringLiveMirroringSystem.StandaloneOwnerId,
                    AuthoringLiveMirroringSystem.StandaloneModuleId,
                    AuthoringLiveMirroringSystem.PrefabContainerStableId))
            {
                continue;
            }

            Transform previous = system.scaleReference;
            system.scaleReference = candidate.transform;
            bool valid =
                LiveMirroringService.HasValidScaleReferenceTopology(
                    system,
                    targets);
            system.scaleReference = previous;

            if (!valid)
                continue;
            if (match != null && match != candidate.transform)
            {
                ambiguous = true;
                return null;
            }

            match = candidate.transform;
        }

        return match;
    }
}
}
#endif
