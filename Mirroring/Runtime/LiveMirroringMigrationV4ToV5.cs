#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using Threadlight.Authoring;

/// <summary>
/// Applies the unified target-pair contract introduced before public release:
/// pairs create an opposite target by default and always keep it mirrored.
/// </summary>
internal sealed class LiveMirroringMigrationV4ToV5 :
    IVersionedDataMigration<AuthoringLiveMirroringSystem>
{
    public int FromVersion => 4;
    public int ToVersion => 5;

    public void Apply(AuthoringLiveMirroringSystem system)
    {
        if (system?.pairs == null) return;
        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];
            if (pair == null) continue;
            pair.createOppositeTarget = true;
            pair.mirrorEnabled = true;
        }
    }
}
}
#endif
