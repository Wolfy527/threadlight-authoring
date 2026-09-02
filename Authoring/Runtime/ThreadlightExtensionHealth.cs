#if UNITY_EDITOR
namespace Threadlight.Authoring
{
using System;

public enum ThreadlightExtensionDiscoveryStatus
{
    Active,
    ActiveBuiltInPreferred,
    DisabledMissingConstructor,
    DisabledConstructionFailed,
    DisabledMetadataInvalid,
    DisabledIdCollision
}

public sealed class ThreadlightExtensionFailure
{
    public string Code { get; }
    public string Phase { get; }
    public string Message { get; }

    public ThreadlightExtensionFailure(string code, string phase, string message)
    {
        Code = code;
        Phase = phase;
        Message = message;
    }
}

/// <summary>
/// Immutable, editor-session-only extension health. ID and Order are absent
/// when discovery failed before valid metadata could be read. Type and assembly
/// names are support information, not stable extension identity.
/// </summary>
public sealed class ThreadlightExtensionHealthDescriptor
{
    public string Id { get; }
    public string TypeName { get; }
    public string AssemblyName { get; }
    public int? Order { get; }
    public string Capability { get; }
    public ThreadlightExtensionDiscoveryStatus DiscoveryStatus { get; }
    public ThreadlightExtensionFailure LastIsolatedFailure { get; }

    public ThreadlightExtensionHealthDescriptor(
        string id,
        string typeName,
        string assemblyName,
        int? order,
        string capability,
        ThreadlightExtensionDiscoveryStatus discoveryStatus,
        ThreadlightExtensionFailure lastIsolatedFailure = null)
    {
        Id = id;
        TypeName = typeName;
        AssemblyName = assemblyName;
        Order = order;
        Capability = capability;
        DiscoveryStatus = discoveryStatus;
        LastIsolatedFailure = lastIsolatedFailure;
    }
}
}
#endif
