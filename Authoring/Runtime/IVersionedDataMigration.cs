namespace Threadlight.Authoring
{
/// <summary>
/// Defines one adjacent, deterministic migration step for serialized data.
/// </summary>
public interface IVersionedDataMigration<T>
{
    int FromVersion { get; }
    int ToVersion { get; }

    void Apply(T target);
}
}
