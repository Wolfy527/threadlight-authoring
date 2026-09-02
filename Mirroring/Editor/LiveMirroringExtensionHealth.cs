namespace Threadlight.Mirroring.Editor
{
using System;
using System.Collections.Generic;
using Threadlight.Authoring;

/// <summary>
/// Read-only, editor-session health for discovered Live Mirroring extensions.
/// Querying the snapshot discovers each extension through its normal registry;
/// it does not create separate callback instances.
/// </summary>
public static class LiveMirroringExtensionHealth
{
    public static IReadOnlyList<ThreadlightExtensionHealthDescriptor> GetSnapshot()
    {
        EnsureDiscovered();
        return LiveMirroringExtensionHealthJournal.Snapshot();
    }

    private static void EnsureDiscovered()
    {
        LiveMirroringProcessorRegistry.GetProcessors();
        LiveMirroringEditorExtensionRegistry.GetInspectorElements();
        LiveMirroringEditorExtensionRegistry.GetValidators();
        try
        {
            LiveMirroringEditorExtensionRegistry.GetTargetBuildContributors();
        }
        catch (InvalidOperationException)
        {
            // Mutation-critical discovery is intentionally still fail-closed.
            // Its disabled descriptors remain available for support tooling.
        }
        try
        {
            LiveMirroringEditorExtensionRegistry.GetSetupOwnershipContributors();
        }
        catch (InvalidOperationException)
        {
            // Ownership discovery also fails closed so an unavailable claim
            // cannot accidentally make another tool's setup editable.
        }
        LiveMirroringEditorExtensionRegistry.GetPreviewContributors();
    }
}
}
