#if UNITY_EDITOR
namespace Threadlight.Authoring
{
using System;
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
}
#endif
