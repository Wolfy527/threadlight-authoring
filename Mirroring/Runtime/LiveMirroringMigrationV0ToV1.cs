#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using Threadlight.Authoring;

internal sealed class LiveMirroringMigrationV0ToV1 :
    IVersionedDataMigration<AuthoringLiveMirroringSystem>
{
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Apply(AuthoringLiveMirroringSystem system)
    {
        if (system.pairs == null)
            return;

        for (int i = 0; i < system.pairs.Length; i++)
        {
            AuthoringLiveMirroringSystem.MirrorPair pair = system.pairs[i];

            if (pair == null)
                continue;

            if (string.IsNullOrWhiteSpace(pair.pairName))
                pair.pairName = $"Mirror Pair {i + 1}";
        }
    }
}
}
#endif
