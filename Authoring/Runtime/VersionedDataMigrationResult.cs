namespace Threadlight.Authoring
{
/// <summary>
/// Describes whether serialized data was changed or deliberately left intact.
/// </summary>
public enum VersionedDataMigrationResult
{
    UpToDate,
    Migrated,
    NewerThanSupported,
    InvalidVersion
}
}
