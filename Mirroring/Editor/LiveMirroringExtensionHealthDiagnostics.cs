namespace Threadlight.Mirroring.Editor
{
using System;
using System.Collections.Generic;
using System.Linq;
using Threadlight.Authoring;

internal static class LiveMirroringExtensionHealthDiagnostics
{
    internal static void Append(
        ICollection<LiveMirroringValidationMessage> messages)
    {
        if (messages == null)
            return;
        IReadOnlyList<ThreadlightExtensionHealthDescriptor> snapshot;
        try
        {
            // This is the normal read-only discovery path. In particular, it
            // exercises mutation-critical target-build discovery before the
            // UI can report the setup as ready.
            snapshot = LiveMirroringExtensionHealth.GetSnapshot();
        }
        catch (Exception)
        {
            AddOnce(
                messages,
                LiveMirroringValidationSeverity.Error,
                "Extension Checks Unavailable",
                "Live Mirroring could not verify its installed extensions. Update or remove the affected extension package, then wait for Unity to reload scripts before building targets.");
            return;
        }

        AppendCriticalCapability(
            messages,
            snapshot,
            LiveMirroringExtensionCapabilities.TargetBuild,
            "Target Build Extension Needs Attention",
            "Live Mirroring cannot safely load one or more target-build extensions. Update or remove the affected extension package, then wait for Unity to reload scripts before building targets.");
        AppendCriticalCapability(
            messages,
            snapshot,
            LiveMirroringExtensionCapabilities.SetupOwnership,
            "Setup Ownership Check Unavailable",
            "Live Mirroring cannot safely verify which tool owns this setup. Update or remove the affected extension package, then wait for Unity to reload scripts. The setup remains read-only until the check succeeds.");

        bool optionalUnavailable = snapshot.Any(value => value != null &&
            !IsMutationCritical(value.Capability) &&
            IsUnavailable(value.DiscoveryStatus));
        if (optionalUnavailable)
            AddOnce(
                messages,
                LiveMirroringValidationSeverity.Warning,
                "Optional Extension Unavailable",
                "Part of an installed Live Mirroring extension could not be loaded. Update or remove the affected extension package, then wait for Unity to reload scripts.");

        bool criticalCallbackFailure = snapshot.Any(value => value != null &&
            value.LastIsolatedFailure != null &&
            string.Equals(
                value.Capability,
                LiveMirroringExtensionCapabilities.SetupOwnership,
                StringComparison.Ordinal));
        if (criticalCallbackFailure)
            AddOnce(
                messages,
                LiveMirroringValidationSeverity.Error,
                "Setup Ownership Check Paused",
                "An installed ownership extension stopped responding, so Live Mirroring cannot safely decide whether this setup is editable. Update or remove the extension, then reopen Unity to retry.");

        bool optionalCallbackFailure = snapshot.Any(value => value != null &&
            value.LastIsolatedFailure != null &&
            !IsMutationCritical(value.Capability));
        if (optionalCallbackFailure)
            AddOnce(
                messages,
                LiveMirroringValidationSeverity.Warning,
                "Optional Extension Paused",
                "An optional Live Mirroring extension stopped responding and was paused for this editor session. Update or remove the extension, then reopen Unity to retry.");
    }

    private static void AppendCriticalCapability(
        ICollection<LiveMirroringValidationMessage> messages,
        IEnumerable<ThreadlightExtensionHealthDescriptor> snapshot,
        string capability,
        string title,
        string explanation)
    {
        if (snapshot.Any(value => value != null &&
                string.Equals(value.Capability, capability,
                    StringComparison.Ordinal) &&
                IsUnavailable(value.DiscoveryStatus)))
            AddOnce(messages, LiveMirroringValidationSeverity.Error,
                title, explanation);
    }

    private static bool IsMutationCritical(string capability) =>
        string.Equals(capability,
            LiveMirroringExtensionCapabilities.TargetBuild,
            StringComparison.Ordinal) ||
        string.Equals(capability,
            LiveMirroringExtensionCapabilities.SetupOwnership,
            StringComparison.Ordinal);

    private static bool IsUnavailable(
        ThreadlightExtensionDiscoveryStatus status) =>
        status != ThreadlightExtensionDiscoveryStatus.Active &&
        status != ThreadlightExtensionDiscoveryStatus.ActiveBuiltInPreferred;

    private static void AddOnce(
        ICollection<LiveMirroringValidationMessage> messages,
        LiveMirroringValidationSeverity severity,
        string title,
        string explanation)
    {
        if (messages.Any(message => message != null &&
                string.Equals(message.Title, title, StringComparison.Ordinal)))
            return;
        messages.Add(new LiveMirroringValidationMessage(
            severity, title, explanation, "@extensions"));
    }
}
}
