namespace Threadlight.Authoring
{
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
/// <summary>
/// Stores one Unity object reference separately from the JSON Builder state.
/// </summary>
public sealed class CreatorPrefabObjectReference
{
    [SerializeField]
    private string propertyPath;

    [SerializeField]
    private UnityEngine.Object value;

    public string PropertyPath => propertyPath;
    public UnityEngine.Object Value => value;

    public CreatorPrefabObjectReference(
        string propertyPath,
        UnityEngine.Object value)
    {
        this.propertyPath = propertyPath ?? string.Empty;
        this.value = value;
    }
}

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("")]
/// <summary>
/// Lightweight customer-side snapshot used to resume a prefab in Prefab
/// Builder without distributing the Builder package itself.
/// </summary>
public sealed class CreatorPrefabSnapshot : CreatorAuthoringComponent
{
    public const int CurrentSchemaVersion = 2;

    // Field names are part of the serialized customer prefab format.
    [SerializeField, HideInInspector]
    private string prefabId;

    [SerializeField, HideInInspector]
    private int prefabSchema = CurrentSchemaVersion;

    [SerializeField, HideInInspector]
    private int builderDataVersion;

    [SerializeField, HideInInspector]
    private string builderPackageVersion;

    [SerializeField, HideInInspector, TextArea]
    private string builderState;

    [SerializeField, HideInInspector]
    private List<CreatorPrefabObjectReference> objectReferences =
        new List<CreatorPrefabObjectReference>();

    [SerializeField, HideInInspector]
    private List<string> builderOwnedPaths = new List<string>();

    [SerializeField, HideInInspector]
    private string snapshotFingerprint;

    public string Id => prefabId;
    public int PrefabSchema => prefabSchema;
    public int BuilderDataVersion => builderDataVersion;
    public string BuilderPackageVersion => builderPackageVersion;
    public string BuilderState => builderState;
    public IReadOnlyList<CreatorPrefabObjectReference> ObjectReferences =>
        objectReferences;
    public IReadOnlyList<string> BuilderOwnedPaths => builderOwnedPaths;
    public string SnapshotFingerprint => snapshotFingerprint;
    public bool HasSnapshot =>
        !string.IsNullOrWhiteSpace(prefabId) &&
        !string.IsNullOrWhiteSpace(builderState);

    public void SetSnapshot(
        string id,
        int dataVersion,
        string packageVersion,
        string state,
        IEnumerable<CreatorPrefabObjectReference> references,
        IEnumerable<string> ownedPaths)
    {
        prefabId = string.IsNullOrWhiteSpace(id)
            ? Guid.NewGuid().ToString("N")
            : id.Trim();
        prefabSchema = CurrentSchemaVersion;
        builderDataVersion = Mathf.Max(0, dataVersion);
        builderPackageVersion = packageVersion?.Trim() ?? string.Empty;
        builderState = state ?? string.Empty;
        objectReferences = references != null
            ? new List<CreatorPrefabObjectReference>(references)
            : new List<CreatorPrefabObjectReference>();
        builderOwnedPaths = ownedPaths != null
            ? new List<string>(ownedPaths)
            : new List<string>();
        snapshotFingerprint = CreatorPrefabSnapshotUtility.ComputeFingerprint(this);
    }

    internal void NormalizeLegacySnapshot()
    {
        prefabId = prefabId?.Trim() ?? string.Empty;
        builderPackageVersion = builderPackageVersion?.Trim() ?? string.Empty;
        builderState = builderState ?? string.Empty;
        objectReferences = objectReferences ??
            new List<CreatorPrefabObjectReference>();
        builderOwnedPaths = builderOwnedPaths ?? new List<string>();
        snapshotFingerprint = snapshotFingerprint?.Trim() ?? string.Empty;
    }

    internal void SetSchemaVersion(int version)
    {
        prefabSchema = version;
    }

    internal void RefreshSnapshotFingerprint()
    {
        snapshotFingerprint = CreatorPrefabSnapshotUtility.ComputeFingerprint(this);
    }
}
}
