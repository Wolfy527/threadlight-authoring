namespace Threadlight.Mirroring.ExtensionExample
{
using System;
using UnityEditor;
using UnityEngine.UIElements;
using Threadlight.EditorUI;

[CustomEditor(typeof(LiveMirroringExtensionExampleSettings))]
public sealed class LiveMirroringExtensionExampleSettingsEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ThreadlightEditorElements.CreateInspectorRoot();
        root.Add(ThreadlightEditorElements.CreateInspectorBanner(
            "Live Mirroring Extension Example",
            "Shows how an extension can add its own validation and preview behavior.",
            ThreadlightEditorTheme.ProjectAccent
        ));

        VisualElement section = ThreadlightEditorElements.CreatePageSection(
            "Example Settings",
            "Try these sample options without changing Live Mirroring's own saved settings.",
            ThreadlightEditorTheme.ProjectAccent,
            out VisualElement content
        );
        ThreadlightEditorTooltipLayer tooltips =
            new ThreadlightEditorTooltipLayer(root);
        // Inspector visual trees can be detached and reattached while docking.
        // Keep the overlay parented and only cancel any pending presentation.
        root.RegisterCallback<DetachFromPanelEvent>(evt => {
            if (evt.target == root)
                tooltips.Hide();
        });

        ThreadlightSerializedForm form = new ThreadlightSerializedForm(
            serializedObject,
            ApplySerializedChange,
            ReadBoolean,
            TooltipForPath,
            (element, title, body) =>
                tooltips.Register(element, title, body)
        );
        form.AddFields(
            content,
            ThreadlightFormField.Toggle(
                "Require Scale Reference",
                "requireScaleReference"),
            ThreadlightFormField.Text(
                "Preview Name Suffix",
                "previewNameSuffix")
        );
        root.Add(section);
        return root;
    }

    private void ApplySerializedChange(
        string path,
        string undoName,
        Action<SerializedProperty> change)
    {
        serializedObject.UpdateIfRequiredOrScript();
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property == null)
            return;
        Undo.RecordObjects(serializedObject.targetObjects, undoName);
        change(property);
        serializedObject.ApplyModifiedProperties();
    }

    private bool ReadBoolean(string path)
    {
        serializedObject.UpdateIfRequiredOrScript();
        return serializedObject.FindProperty(path)?.boolValue ?? false;
    }

    private static string TooltipForPath(string path)
    {
        switch (path)
        {
            case "requireScaleReference":
                return "Require the Live Mirroring System to have a Prefab Scale Reference before sample validation passes.";
            case "previewNameSuffix":
                return "Append this text to sample scene-preview object names. Leave it empty to keep their original names.";
            default:
                return string.Empty;
        }
    }
}
}
