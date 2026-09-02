namespace Threadlight.Authoring
{
using System;

/// <summary>
/// Migrates the Prefab ID container format. Builder state and individual module
/// settings are migrated separately by ThreadLight Builder.
/// </summary>
public static class CreatorPrefabSnapshotMigrationService
{
    public static VersionedDataMigrationResult TryMigrate(CreatorPrefabSnapshot prefabId)
    {
        if (prefabId == null)
            return VersionedDataMigrationResult.InvalidVersion;
        if (prefabId.PrefabSchema < 0)
            return VersionedDataMigrationResult.InvalidVersion;
        if (prefabId.PrefabSchema > CreatorPrefabSnapshot.CurrentSchemaVersion)
            return VersionedDataMigrationResult.NewerThanSupported;
        if (prefabId.PrefabSchema == CreatorPrefabSnapshot.CurrentSchemaVersion)
            return VersionedDataMigrationResult.UpToDate;

        if (prefabId.PrefabSchema == 0)
        {
            prefabId.NormalizeLegacySnapshot();
            prefabId.SetSchemaVersion(1);
        }
        if (prefabId.PrefabSchema == 1)
        {
            prefabId.NormalizeLegacySnapshot();
            prefabId.RefreshSnapshotFingerprint();
            prefabId.SetSchemaVersion(2);
        }
        return VersionedDataMigrationResult.Migrated;
    }

    public static bool HasValidFingerprint(CreatorPrefabSnapshot prefabId)
    {
        if (prefabId == null ||
            prefabId.PrefabSchema != CreatorPrefabSnapshot.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(prefabId.SnapshotFingerprint))
        {
            return false;
        }

        return string.Equals(
            prefabId.SnapshotFingerprint,
            CreatorPrefabSnapshotUtility.ComputeFingerprint(prefabId),
            StringComparison.Ordinal
        );
    }

}
}
