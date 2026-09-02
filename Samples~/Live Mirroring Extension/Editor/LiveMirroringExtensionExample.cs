namespace Threadlight.Mirroring.ExtensionExample
{
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Threadlight.Mirroring;
using Threadlight.Mirroring.Editor;

public sealed class ExampleValidationContributor :
    ILiveMirroringValidationContributor
{
    public string ContributorId =>
        "example.live-mirroring.validation";
    public int Order => 1000;

    public void Validate(
        SerializedObject serializedSystem,
        List<LiveMirroringValidationMessage> messages)
    {
        AuthoringLiveMirroringSystem system = serializedSystem?.targetObject as
            AuthoringLiveMirroringSystem;
        LiveMirroringExtensionExampleSettings settings =
            system?.GetComponent<LiveMirroringExtensionExampleSettings>();
        if (settings != null && settings.requireScaleReference &&
            system.scaleReference == null)
        {
            messages.Add(new LiveMirroringValidationMessage(
                LiveMirroringValidationSeverity.Error,
                "Example extension needs a scale reference",
                "Assign Scale Reference or disable the sample requirement.",
                "scaleReference"));
        }
    }
}

public sealed class ExampleProcessor : ILiveMirroringProcessor
{
    public string ProcessorId => "example.live-mirroring.processor";
    public int Order => 1000;
    public LiveMirroringProcessingStage Stage =>
        LiveMirroringProcessingStage.AfterCore;

    public void Process(AuthoringLiveMirroringSystem system)
    {
        if (system == null ||
            system.GetComponent<LiveMirroringExtensionExampleSettings>() == null)
            return;

        // Apply stateless editor-time behavior here. Persist configuration on
        // your own component rather than on the processor instance.
    }
}

public sealed class ExamplePreviewContributor :
    ILiveMirroringPreviewContributor
{
    public string ContributorId => "example.live-mirroring.preview";
    public int Order => 1000;

    public void OnPreviewCreated(LiveMirroringPreviewContext context)
    {
        LiveMirroringExtensionExampleSettings settings =
            context?.System?.GetComponent<
                LiveMirroringExtensionExampleSettings>();
        if (settings == null || context.PreviewInstance == null ||
            string.IsNullOrWhiteSpace(settings.previewNameSuffix))
            return;
        context.PreviewInstance.name += settings.previewNameSuffix.Trim();
    }

    public void UpdatePreview(LiveMirroringPreviewContext context) { }
}

public sealed class ExampleTargetBuildContributor :
    ILiveMirroringTargetBuildContributor
{
    public string ContributorId => "example.live-mirroring.target-build";
    public int Order => 1000;

    public int Apply(LiveMirroringTargetBuildContext context)
    {
        if (context?.System == null ||
            context.System.GetComponent<
                LiveMirroringExtensionExampleSettings>() == null)
            return 0;

        // Apply owned target behavior here. Use Undo for every mutation and
        // return the number of owned changes. Throwing rolls back the build.
        return 0;
    }
}

public static class ExampleExtensionHealthMenu
{
    [MenuItem("Tools/Live Mirroring Extension Example/Log Health")]
    private static void LogHealth()
    {
        foreach (var entry in LiveMirroringExtensionHealth.GetSnapshot())
            Debug.Log($"{entry.Capability}: {entry.Id} - " +
                      entry.DiscoveryStatus);
    }
}
}
