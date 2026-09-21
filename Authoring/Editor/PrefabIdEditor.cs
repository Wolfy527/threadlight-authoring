namespace Threadlight.Authoring.Editor
{
using Threadlight.EditorUI;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(CreatorPrefabSnapshot))]
public sealed class PrefabIdEditor : UnityEditor.Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ThreadlightEditorElements.CreateInspectorRoot();
        root.Add(ThreadlightEditorElements.CreateInspectorBanner(
            "Prefab ID",
            "Keeps this prefab connected to its saved ThreadLight Builder settings.",
            ThreadlightEditorTheme.ProjectAccent
        ));
        VisualElement section = ThreadlightEditorElements.CreatePageSection(
            "Saved Setup",
            "Identifies the saved setup and Builder version. ThreadLight removes this component during Play Mode and avatar upload.",
            ThreadlightEditorTheme.ProjectAccent,
            out VisualElement details
        );
        RebuildDetails(details);
        root.TrackSerializedObjectValue(
            serializedObject,
            _ => RebuildDetails(details));
        root.Add(section);
        return root;
    }

    private void RebuildDetails(VisualElement details)
    {
        if (details == null)
            return;

        serializedObject.UpdateIfRequiredOrScript();
        CreatorPrefabSnapshot prefabId = target as CreatorPrefabSnapshot;
        details.Clear();
        details.Add(ThreadlightEditorElements.CreateReadOnlyValue(
            "Prefab ID",
            prefabId?.Id
        ));
        details.Add(ThreadlightEditorElements.CreateReadOnlyValue(
            "Prefab Format Version",
            (prefabId?.PrefabSchema ?? 0).ToString()
        ));
        details.Add(ThreadlightEditorElements.CreateReadOnlyValue(
            "Builder Settings Version",
            (prefabId?.BuilderDataVersion ?? 0).ToString()
        ));

        if (prefabId != null &&
            !string.IsNullOrWhiteSpace(prefabId.BuilderPackageVersion))
        {
            details.Add(ThreadlightEditorElements.CreateReadOnlyValue(
                "Created With",
                $"ThreadLight Builder {prefabId.BuilderPackageVersion}"
            ));
        }
    }
}
}
