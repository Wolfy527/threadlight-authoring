#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using System;
using System.Collections.Generic;
using System.Linq;
using Threadlight.Authoring;

public static class LiveMirroringExtensionCapabilities
{
    public const string Processor = "threadlight.mirroring.processor";
    public const string ProcessorBeforeCore =
        "threadlight.mirroring.processor.before-core";
    public const string ProcessorAfterCore =
        "threadlight.mirroring.processor.after-core";
    public const string Inspector = "threadlight.mirroring.inspector";
    public const string Validation = "threadlight.mirroring.validation";
    public const string TargetBuild = "threadlight.mirroring.target-build";
    public const string SetupOwnership =
        "threadlight.mirroring.setup-ownership";
    public const string Preview = "threadlight.mirroring.preview";
}

internal static class LiveMirroringExtensionHealthJournal
{
    private static readonly Dictionary<string,
        List<ThreadlightExtensionHealthDescriptor>> discovery =
        new Dictionary<string, List<ThreadlightExtensionHealthDescriptor>>(
            StringComparer.Ordinal);
    private static readonly Dictionary<string, ThreadlightExtensionFailure>
        isolatedFailures =
        new Dictionary<string, ThreadlightExtensionFailure>(
            StringComparer.Ordinal);

    internal static void ReplaceDiscovery(
        IEnumerable<string> capabilities,
        IEnumerable<ThreadlightExtensionHealthDescriptor> descriptors)
    {
        foreach (string capability in (capabilities ??
                     Array.Empty<string>()).Where(value =>
                         !string.IsNullOrWhiteSpace(value)).Distinct(
                         StringComparer.Ordinal))
            discovery.Remove(capability);
        foreach (IGrouping<string, ThreadlightExtensionHealthDescriptor> group in
                 (descriptors ??
                  Array.Empty<ThreadlightExtensionHealthDescriptor>())
                 .Where(value => value != null &&
                     !string.IsNullOrWhiteSpace(value.Capability))
                 .GroupBy(value => value.Capability,
                     StringComparer.Ordinal))
            discovery[group.Key] = group.ToList();
    }

    internal static void RecordIsolatedFailure(
        string capability,
        string id,
        Type type,
        string phase,
        Exception exception)
    {
        string key = Key(capability, id, TypeName(type));
        isolatedFailures[key] = new ThreadlightExtensionFailure(
            "threadlight.mirroring.extension.callback-failed",
            phase,
            SafeMessage(exception));
    }

    internal static IReadOnlyList<ThreadlightExtensionHealthDescriptor> Snapshot()
    {
        ThreadlightExtensionHealthDescriptor[] result = discovery.Values
            .SelectMany(value => value)
            .Select(value => new ThreadlightExtensionHealthDescriptor(
                value.Id,
                value.TypeName,
                value.AssemblyName,
                value.Order,
                value.Capability,
                value.DiscoveryStatus,
                isolatedFailures.TryGetValue(Key(
                    value.Capability, value.Id, value.TypeName),
                    out ThreadlightExtensionFailure failure)
                    ? failure
                    : null))
            .OrderBy(value => value.Capability, StringComparer.Ordinal)
            .ThenBy(value => value.Order ?? int.MaxValue)
            .ThenBy(value => value.Id ?? string.Empty,
                StringComparer.Ordinal)
            .ThenBy(value => value.TypeName ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(result);
    }

    internal static void Clear(params string[] capabilities)
    {
        HashSet<string> removed = new HashSet<string>(
            capabilities ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (string capability in removed)
            discovery.Remove(capability);
        foreach (string key in isolatedFailures.Keys.Where(key =>
                     removed.Any(capability => key.StartsWith(
                         capability + "|", StringComparison.Ordinal)))
                 .ToArray())
            isolatedFailures.Remove(key);
    }

    internal static ThreadlightExtensionHealthDescriptor Descriptor(
        Type type,
        string id,
        int? order,
        string capability,
        ThreadlightExtensionDiscoveryStatus status) =>
        new ThreadlightExtensionHealthDescriptor(
            id,
            TypeName(type),
            type?.Assembly?.GetName().Name,
            order,
            capability,
            status);

    private static string Key(string capability, string id, string typeName) =>
        (capability ?? string.Empty) + "|" + (id ?? string.Empty) + "|" +
        (typeName ?? string.Empty);

    private static string TypeName(Type type) =>
        type?.FullName ?? type?.Name;

    private static string SafeMessage(Exception exception)
    {
        const int maximumLength = 512;
        string message = exception?.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            return "The extension failed without an error message.";
        message = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= maximumLength
            ? message
            : message.Substring(0, maximumLength) + "...";
    }
}
}
#endif
