namespace Threadlight.Mirroring.Editor {
using Threadlight.EditorUI;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
internal static class LiveMirroringSetupElements {
    public static LiveMirroringSetupCard CreateSection(string title, string description,
        ThreadlightEditorTone tone, string kind = "SECTION", bool expanded = true,
        Action<bool> expansionChanged = null) {
        ThreadlightEditorPalette palette = ThreadlightEditorTheme.Palette(tone);
        return new LiveMirroringSetupCard(title, description, palette.Accent,
            expanded, kind, palette.Fill, expansionChanged);
    }
    public static VisualElement CreateActionRow() {
        VisualElement row = ThreadlightEditorElements.CreateActionStrip();
        row.AddToClassList("threadlight-mirroring-action-row");
        return row;
    }
    public static Button CreateButton(string text, Action clicked, bool primary = false,
        bool danger = false, Color? accent = null) {
        Color interactionAccent = primary && !danger
            ? ThreadlightEditorTheme.WorkspacePrefabAccent
            : accent ?? ThreadlightEditorTheme.WorkspacePrefabAccent;
        Button button = primary && !danger ? ThreadlightEditorElements.CreatePrimaryButton(text, clicked,
                interactionAccent) :
            ThreadlightEditorElements.CreateCompactButton(text, clicked, danger,
                interactionAccent);
        button.AddToClassList("threadlight-mirroring-action-button");
        return button;
    }
    public static MessageType ToMessageType(LiveMirroringValidationSeverity severity) =>
        severity == LiveMirroringValidationSeverity.Error ? MessageType.Error :
        severity == LiveMirroringValidationSeverity.Warning ? MessageType.Warning : MessageType.Info;
}
internal class LiveMirroringSetupCard : ThreadlightDisclosureCard {
    protected readonly Label Badge;
    public LiveMirroringSetupCard(string title, string description, Color? accent = null, bool expanded = true,
        string kind = "SECTION", Color? fill = null,
        Action<bool> expansionChanged = null)
        : base(
            title,
            description,
            accent ?? ThreadlightEditorTheme.WorkspacePrefabAccent,
            fill ?? Color.Lerp(
                ThreadlightEditorTheme.BackgroundDark,
                ThreadlightEditorTheme.ModuleStandard,
                .1f),
            expanded,
            kind,
            "threadlight-mirroring-card-description",
            expansionChanged) {
        AddToClassList("threadlight-mirroring-card");
        Badge = ThreadlightEditorElements.CreateStatusBadge(
            "threadlight-mirroring-validation-badge");
        Accessories.Add(Badge);
    }
    public Color InteractionAccent => Accent;
    public void SetValidationState(int errors, int warnings) {
        bool visible = errors > 0 || warnings > 0;
        if (!visible) {
            ThreadlightEditorElements.UpdateStatusBadge(
                Badge, string.Empty, ThreadlightEditorTheme.TextDim, false);
            return;
        }
        bool error = errors > 0;
        Color color = error ? ThreadlightEditorTheme.Error : ThreadlightEditorTheme.Warning;
        string text = error ? $"{errors} ERROR{(errors == 1 ? "" : "S")}" :
            $"{warnings} WARNING{(warnings == 1 ? "" : "S")}";
        ThreadlightEditorElements.UpdateStatusBadge(Badge, text, color);
    }
}
internal sealed class LiveMirroringTargetCard : LiveMirroringSetupCard {
    public Button RemoveButton { get; }
    public LiveMirroringTargetCard(string title, bool expanded, Action<bool> changed, Action remove)
        : base(title, null, ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Feature).Accent,
            expanded, "TARGET", ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Feature).Fill,
            changed) {
        AddToClassList("threadlight-mirroring-target-card");
        Heading.text = title;
        Badge.AddToClassList("threadlight-mirroring-target-validation-badge");
        RemoveButton = LiveMirroringSetupElements.CreateButton("Remove", remove, false, true,
            InteractionAccent);
        RemoveButton.AddToClassList("threadlight-mirroring-remove-target");
        Accessories.Add(RemoveButton);
    }
    public void SetTitle(string title) => Heading.text = string.IsNullOrWhiteSpace(title) ? "Target" : title.Trim();
}
}
