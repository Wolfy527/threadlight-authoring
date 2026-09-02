#if UNITY_EDITOR
namespace Threadlight.Authoring
{
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum ThreadlightAuthoringValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Immutable host information supplied to validation-only creator extensions.
/// The contributor owns any additional configuration on its own components or
/// assets; this context deliberately exposes no build or mutation services.
/// </summary>
public sealed class ThreadlightAuthoringValidationContext
{
    public string ToolId { get; }
    public GameObject AuthoringRoot { get; }

    public ThreadlightAuthoringValidationContext(
        string toolId,
        GameObject authoringRoot)
    {
        if (string.IsNullOrWhiteSpace(toolId) || toolId != toolId.Trim())
            throw new ArgumentException(
                "Authoring tool IDs must be non-blank and trimmed.",
                nameof(toolId));
        ToolId = toolId;
        AuthoringRoot = authoringRoot;
    }
}

/// <summary>One immutable diagnostic with a source stamped by its host.</summary>
public sealed class ThreadlightAuthoringValidationDiagnostic
{
    public ThreadlightAuthoringValidationSeverity Severity { get; }
    public string SourceId { get; }
    public string Code { get; }
    public string Message { get; }
    public string PropertyPath { get; }
    public string Remediation { get; }

    internal ThreadlightAuthoringValidationDiagnostic(
        ThreadlightAuthoringValidationSeverity severity,
        string sourceId,
        string code,
        string message,
        string propertyPath,
        string remediation)
    {
        Severity = severity;
        SourceId = sourceId;
        Code = code;
        Message = message;
        PropertyPath = propertyPath;
        Remediation = remediation;
    }
}

/// <summary>
/// Contributor-owned diagnostic output. SourceId is intentionally absent: the
/// registry stamps it from the discovered contributor's permanent ID.
/// </summary>
public interface IThreadlightAuthoringDiagnosticSink
{
    void Add(
        ThreadlightAuthoringValidationSeverity severity,
        string code,
        string message,
        string propertyPath = null,
        string remediation = null);
}

/// <summary>
/// Public validation-only extension point shared by creator authoring tools.
/// Implementations must not mutate scenes, assets, serialized data, or Undo.
/// </summary>
public interface IThreadlightAuthoringValidationContributor
{
    string ContributorId { get; }
    int Order { get; }
    void Validate(
        ThreadlightAuthoringValidationContext context,
        IThreadlightAuthoringDiagnosticSink diagnostics);
}

/// <summary>
/// Deterministically discovers and isolates public authoring validators. A bad
/// extension becomes an advisory diagnostic and never disables core checks.
/// </summary>
public static class ThreadlightAuthoringValidationRegistry
{
    private const string RegistrySourceId =
        "threadlight.authoring.validation-registry";
    private static Descriptor[] descriptors;
    private static ThreadlightAuthoringValidationDiagnostic[] discoveryWarnings;

    public static IReadOnlyList<ThreadlightAuthoringValidationDiagnostic> Collect(
        ThreadlightAuthoringValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        EnsureDiscovered();
        List<ThreadlightAuthoringValidationDiagnostic> diagnostics =
            new List<ThreadlightAuthoringValidationDiagnostic>(
                discoveryWarnings.Length);
        diagnostics.AddRange(discoveryWarnings);
        for (int index = 0; index < descriptors.Length; index++)
        {
            Descriptor descriptor = descriptors[index];
            DiagnosticSink sink = new DiagnosticSink(
                descriptor.Id, diagnostics);
            try
            {
                descriptor.Instance.Validate(context, sink);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Warning(
                    descriptor.Id,
                    "threadlight.authoring.validation.callback-failed",
                    "Authoring validation contributor '" + descriptor.Id +
                    "' could not complete its checks: " +
                    exception.GetBaseException().Message));
            }
        }
        return Array.AsReadOnly(diagnostics.ToArray());
    }

    public static void Refresh()
    {
        descriptors = null;
        discoveryWarnings = null;
    }

    private static void EnsureDiscovered()
    {
        if (descriptors != null && discoveryWarnings != null)
            return;
        List<ThreadlightAuthoringValidationDiagnostic> warnings =
            new List<ThreadlightAuthoringValidationDiagnostic>();
        List<Descriptor> found = new List<Descriptor>();
        foreach (Type type in TypeCache
                     .GetTypesDerivedFrom<IThreadlightAuthoringValidationContributor>()
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.FullName,
                         StringComparer.Ordinal))
        {
            if (type.IsAbstract || type.IsInterface)
                continue;
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                warnings.Add(DiscoveryWarning(
                    "threadlight.authoring.validation.constructor-missing",
                    "Authoring validation contributor '" + type.FullName +
                    "' was disabled because it has no public parameterless constructor."));
                continue;
            }
            IThreadlightAuthoringValidationContributor instance;
            try
            {
                instance = Activator.CreateInstance(type) as
                    IThreadlightAuthoringValidationContributor;
            }
            catch (Exception exception)
            {
                warnings.Add(DiscoveryWarning(
                    "threadlight.authoring.validation.constructor-failed",
                    "Authoring validation contributor '" + type.FullName +
                    "' was disabled because construction failed: " +
                    exception.GetBaseException().Message));
                continue;
            }
            try
            {
                string id = instance?.ContributorId;
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
                    throw new InvalidOperationException(
                        "ContributorId must be non-blank and trimmed.");
                found.Add(new Descriptor(instance, id, instance.Order));
            }
            catch (Exception exception)
            {
                warnings.Add(DiscoveryWarning(
                    "threadlight.authoring.validation.metadata-invalid",
                    "Authoring validation contributor '" + type.FullName +
                    "' was disabled because its metadata is invalid: " +
                    exception.GetBaseException().Message));
            }
        }

        List<Descriptor> resolved = new List<Descriptor>();
        foreach (IGrouping<string, Descriptor> group in found
                     .GroupBy(value => value.Id, StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            Descriptor[] matches = group.ToArray();
            if (matches.Length == 1)
            {
                resolved.Add(matches[0]);
                continue;
            }
            warnings.Add(DiscoveryWarning(
                "threadlight.authoring.validation.id-collision",
                matches.Length + " authoring validation contributors use ID '" +
                group.Key + "'. All conflicting contributors were disabled."));
        }
        descriptors = resolved
            .OrderBy(value => value.Order)
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        discoveryWarnings = warnings.ToArray();
    }

    private static ThreadlightAuthoringValidationDiagnostic DiscoveryWarning(
        string code,
        string message) => Warning(RegistrySourceId, code, message);

    private static ThreadlightAuthoringValidationDiagnostic Warning(
        string sourceId,
        string code,
        string message) => new ThreadlightAuthoringValidationDiagnostic(
            ThreadlightAuthoringValidationSeverity.Warning,
            sourceId,
            code,
            message,
            null,
            "Update or remove the affected validation extension, then refresh its registry.");

    private sealed class Descriptor
    {
        internal IThreadlightAuthoringValidationContributor Instance { get; }
        internal string Id { get; }
        internal int Order { get; }

        internal Descriptor(
            IThreadlightAuthoringValidationContributor instance,
            string id,
            int order)
        {
            Instance = instance;
            Id = id;
            Order = order;
        }
    }

    private sealed class DiagnosticSink : IThreadlightAuthoringDiagnosticSink
    {
        private readonly string sourceId;
        private readonly ICollection<ThreadlightAuthoringValidationDiagnostic>
            diagnostics;

        internal DiagnosticSink(
            string sourceId,
            ICollection<ThreadlightAuthoringValidationDiagnostic> diagnostics)
        {
            this.sourceId = sourceId;
            this.diagnostics = diagnostics;
        }

        public void Add(
            ThreadlightAuthoringValidationSeverity severity,
            string code,
            string message,
            string propertyPath = null,
            string remediation = null)
        {
            if (string.IsNullOrWhiteSpace(code) || code != code.Trim())
                throw new ArgumentException(
                    "Diagnostic codes must be non-blank and trimmed.",
                    nameof(code));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException(
                    "Diagnostic messages must be non-blank.",
                    nameof(message));
            diagnostics.Add(new ThreadlightAuthoringValidationDiagnostic(
                severity,
                sourceId,
                code,
                message.Trim(),
                string.IsNullOrWhiteSpace(propertyPath)
                    ? null
                    : propertyPath.Trim(),
                string.IsNullOrWhiteSpace(remediation)
                    ? null
                    : remediation.Trim()));
        }
    }
}
}
#endif
