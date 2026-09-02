namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private VisualElement CreateSetupSelector() {
        LiveMirroringSetupCard card = CreateWorkspaceSection("Setup Selection",
            "Use an existing setup or choose a prefab root to create one.",
            ThreadlightEditorTone.Core,
            "SETUP");
        systemField = Field("Current Setup", typeof(AuthoringLiveMirroringSystem), currentSystem,
            card.InteractionAccent);
        systemField.RegisterValueChangedCallback(evt => SetSystem(evt.newValue as AuthoringLiveMirroringSystem));
        card.Add(AddTooltip(systemField, "Current Setup", "Choose an existing Live Mirroring setup to edit."));
        rootField = Field("Prefab Root", typeof(GameObject), candidateRoot,
            card.InteractionAccent);
        rootField.RegisterValueChangedCallback(evt => {
            candidateRoot = evt.newValue as GameObject;
            if (candidateRoot == null || candidateScaleReference == candidateRoot.transform ||
                candidateScaleReference != null && !candidateScaleReference.IsChildOf(candidateRoot.transform))
                candidateScaleReference = null;
            creationError = null;
            if (currentSystem == null) RebuildWorkspace();
        });
        card.Add(AddTooltip(rootField, "Prefab Root", "Choose the root that should contain the setup and generated targets."));
        VisualElement actions = CreateActionRow();
        actions.Add(AddTooltip(CreateButton("Use Selection", UseSelection, true, false,
                card.InteractionAccent),
            "Use Selection", "Use the selected setup, or treat the selected object as the prefab root."));
        if (currentSystem != null) actions.Add(AddTooltip(CreateButton("Select Setup Object", SelectSetupObject,
                false, false, card.InteractionAccent),
            "Select Setup Object", "Select the EditorOnly object that stores this setup."));
        card.Add(actions);
        return card;
    }
    private static ObjectField Field(string label, System.Type type, Object value,
        Color interactionAccent) {
        ObjectField field = new ObjectField(label) { objectType = type, allowSceneObjects = true };
        ThreadlightEditorElements.StyleField(field,
            () => interactionAccent,
            () => interactionAccent);
        field.SetValueWithoutNotify(value);
        return field;
    }
}
}
