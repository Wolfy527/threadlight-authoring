namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private VisualElement CreateSetupSelector() {
        LiveMirroringSetupCard card = CreateWorkspaceSection("Setup Selection",
            "Select an existing setup or choose a Prefab Root for a new setup.",
            ThreadlightEditorTone.Core,
            "SETUP");
        systemField = Field("Current Setup", typeof(AuthoringLiveMirroringSystem), currentSystem,
            card.InteractionAccent);
        systemField.RegisterValueChangedCallback(evt => SetSystem(evt.newValue as AuthoringLiveMirroringSystem));
        card.Add(AddTooltip(systemField, "Current Setup", "Select the Live Mirroring setup to edit."));
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
        card.Add(AddTooltip(rootField, "Prefab Root", "Selects the Prefab Root that owns this setup and its generated targets."));
        VisualElement actions = CreateActionRow();
        actions.AddToClassList("threadlight-mirroring-setup-actions");
        actions.Add(AddTooltip(CreateButton("Use Selection", UseSelection, true, false,
                card.InteractionAccent),
            "Use Selection", "Uses the selected setup or selected object as the Prefab Root."));
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
    private void ArrangeWorkspaceColumns() {
        var cards = new List<VisualElement>();
        foreach (VisualElement child in workspace.Children())
            if (child is LiveMirroringSetupCard) cards.Add(child);
        if (cards.Count == 0) return;
        var grid = new ThreadlightColumnLayout { name = "threadlight-mirroring-section-grid" };
        foreach (VisualElement card in cards) {
            var slot = new VisualElement();
            slot.style.minWidth = 0;
            slot.style.flexShrink = 0;
            slot.Add(card);
            grid.AddSlot(slot);
        }
        workspace.Add(grid);
        grid.BindContents(cards);
    }
}
}
