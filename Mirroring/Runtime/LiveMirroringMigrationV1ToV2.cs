#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using Threadlight.Authoring;

using UnityEngine;

internal sealed class LiveMirroringMigrationV1ToV2 :
    IVersionedDataMigration<AuthoringLiveMirroringSystem>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public void Apply(AuthoringLiveMirroringSystem system)
    {
        if (string.IsNullOrWhiteSpace(system.constraintTargetsObjectName))
        {
            system.constraintTargetsObjectName =
                AuthoringLiveMirroringSystem.DefaultConstraintTargetsObjectName;
        }

        system.addVrcfuryArmatureLinks = true;
        if (system.pairs == null)
            return;

        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
            if (pair == null)
                continue;

            pair.sourceBone = HumanBodyBones.RightHand;
            pair.mirroredBone = HumanBodyBones.LeftHand;
        }
    }
}
}
#endif
