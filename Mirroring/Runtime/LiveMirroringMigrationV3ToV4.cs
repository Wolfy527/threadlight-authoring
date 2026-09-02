#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using Threadlight.Authoring;
using UnityEngine;

/// <summary>
/// Aligns existing lightweight target setups with the Builder's default
/// prefab-container constraint behavior.
/// </summary>
internal sealed class LiveMirroringMigrationV3ToV4 :
    IVersionedDataMigration<AuthoringLiveMirroringSystem>
{
    public int FromVersion => 3;
    public int ToVersion => 4;

    public void Apply(AuthoringLiveMirroringSystem system)
    {
        if (system == null) return;
        system.addParentConstraintToPrefabContainer = true;
        system.prefabContainerParentConstraint = null;
        system.SetPrefabContainerParentConstraintOwnership(false);
        // Version 3 generated names without a prefix. Keep that path stable;
        // newly created version 4 setups use the Builder's "Prefab" default.
        system.targetNamePrefix = string.Empty;
        if (string.IsNullOrWhiteSpace(system.sourceSideLabel))
            system.sourceSideLabel = "R";
        if (string.IsNullOrWhiteSpace(system.mirroredSideLabel))
            system.mirroredSideLabel = "L";
        if (string.IsNullOrWhiteSpace(system.sourceFolderName))
            system.sourceFolderName = "SETUP - Move These";
        if (string.IsNullOrWhiteSpace(system.mirroredFolderName))
            system.mirroredFolderName =
                "AUTO - Mirrored Targets - Do Not Touch";
        system.removeUnusedGeneratedTargets = true;
        system.targetLocalPosition = Vector3.zero;
        system.targetLocalEulerRotation = Vector3.zero;
        system.targetLocalScale = Vector3.one;
        system.applyDefaultTransformToExistingTargets = false;
        if (system.pairs != null)
            for (int i = 0; i < system.pairs.Length; i++)
            {
                AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
                if (pair == null) continue;
                pair.createOppositeTarget = pair.mirrorEnabled;
                pair.useGlobalSideLabels = true;
                pair.sourceSideLabel = "R";
                pair.mirroredSideLabel = "L";
            }
    }
}
}
#endif
