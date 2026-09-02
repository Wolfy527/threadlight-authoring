namespace Threadlight.EditorUI {
using UnityEngine;

/// <summary>
/// Semantic color roles shared by every Prefab Tools editor surface. Callers
/// choose what a surface means instead of assembling a one-off palette.
/// </summary>
public enum ThreadlightEditorTone {
    Standard,
    Core,
    Feature,
    External,
    Project,
    Folder,
    Export,
    Review
}

public readonly struct ThreadlightEditorPalette {
    public readonly Color Fill;
    public readonly Color Hover;
    public readonly Color Accent;
    public readonly Color Border;
    public ThreadlightEditorPalette(Color fill, Color hover, Color accent, Color border) {
        Fill = fill;
        Hover = hover;
        Accent = accent;
        Border = border;
    }
}

public static class ThreadlightEditorTheme {
    public const string HeaderLogoGuid = "7a5d4d24c0f54aa6b70d98fd7bb774c1";
    public const string HeaderLogoPath =
        "Packages/com.wolfyvr.threadlight.authoring/Threadlight Wordmark.png";
    public const string CompactLogoGuid = "c71d6b840cd24e238c50c7a34bd7e3ad";
    public const string CompactLogoPath =
        "Packages/com.wolfyvr.threadlight.authoring/Threadlight Compact Mark.png";
    public static readonly Color Accent = new Color(0.82f, 0.42f, 0.16f, 1f);
    public static readonly Color AccentBright = new Color(0.98f, 0.69f, 0.31f, 1f);
    public static readonly Color AccentDark = new Color(0.42f, 0.18f, 0.08f, 1f);
    public static readonly Color AccentDim = new Color(0.82f, 0.42f, 0.16f, 0.26f);
    public static readonly Color AccentSoft = new Color(0.82f, 0.42f, 0.16f, 0.11f);
    public static readonly Color HighlightFill =
        new Color(0.96f, 0.56f, 0.24f, 0.18f);
    public static readonly Color HighlightBorder =
        new Color(1.00f, 0.73f, 0.40f, 0.94f);
    public static readonly Color HighlightAccent =
        new Color(1.00f, 0.80f, 0.50f, 1f);
    public static readonly Color Background = new Color(0.045f, 0.036f, 0.033f, 1f);
    public static readonly Color BackgroundDark = new Color(0.020f, 0.017f, 0.016f, 1f);
    public static readonly Color HeaderLeft = new Color(0.130f, 0.065f, 0.040f, 1f);
    public static readonly Color HeaderMid = new Color(0.075f, 0.052f, 0.043f, 1f);
    public static readonly Color HeaderRight = new Color(0.035f, 0.044f, 0.039f, 1f);
    public static readonly Color HeaderDescription = new Color(0.027f, 0.024f, 0.022f, 0.80f);
    public static readonly Color ModuleLeft = new Color(0.120f, 0.065f, 0.040f, 1f);
    public static readonly Color ModuleMid = new Color(0.072f, 0.051f, 0.043f, 1f);
    public static readonly Color ModuleRight = new Color(0.040f, 0.045f, 0.040f, 1f);
    public static readonly Color ModuleCore =
        new Color(0.105f, 0.072f, 0.050f, 1f);
    public static readonly Color ModuleCoreHover =
        new Color(0.165f, 0.105f, 0.070f, 1f);
    public static readonly Color ModuleStandard =
        new Color(0.085f, 0.068f, 0.058f, 1f);
    public static readonly Color ModuleStandardHover =
        new Color(0.145f, 0.105f, 0.075f, 1f);
    public static readonly Color ModuleInteractive =
        new Color(0.095f, 0.085f, 0.065f, 1f);
    public static readonly Color ModuleInteractiveHover =
        new Color(0.145f, 0.130f, 0.100f, 1f);
    public static readonly Color ModuleIntegration =
        new Color(0.105f, 0.100f, 0.035f, 1f);
    public static readonly Color ModuleIntegrationHover =
        new Color(0.160f, 0.150f, 0.055f, 1f);
    public static readonly Color ModuleProject =
        new Color(0.050f, 0.100f, 0.080f, 1f);
    public static readonly Color ModuleProjectHover =
        new Color(0.075f, 0.150f, 0.120f, 1f);
    public static readonly Color ProjectAccent =
        new Color(0.38f, 0.64f, 0.56f, 1f);
    public static readonly Color AuroraViolet =
        new Color(0.88f, 0.42f, 0.17f, 1f);
    public static readonly Color AuroraCyan =
        new Color(0.965f, 0.886f, 0.31f, 1f);
    public static readonly Color AuroraMagenta =
        new Color(0.93f, 0.88f, 0.73f, 1f);
    public static readonly Color WorkspacePrefabAccent =
        new Color(0.88f, 0.48f, 0.22f, 1f);
    public static readonly Color WorkspaceFolderAccent =
        new Color(0.72f, 0.32f, 0.20f, 1f);
    public static readonly Color WorkspaceExportAccent =
        new Color(0.42f, 0.62f, 0.36f, 1f);
    public static readonly Color WorkspaceReviewAccent =
        new Color(0.88f, 0.48f, 0.22f, 1f);
    public static readonly Color WorkspaceSettingsAccent =
        new Color(0.68f, 0.56f, 0.44f, 1f);
    public static readonly Color GlassSurface =
        new Color(0.105f, 0.078f, 0.060f, 0.78f);
    public static readonly Color GlassHover =
        new Color(0.155f, 0.115f, 0.080f, 0.90f);
    public static readonly Color Panel = new Color(0.082f, 0.065f, 0.055f, 1f);
    public static readonly Color PanelTop = new Color(0.110f, 0.082f, 0.062f, 1f);
    public static readonly Color PanelBottom = new Color(0.055f, 0.047f, 0.042f, 1f);
    public static readonly Color PanelHeader = new Color(0.46f, 0.27f, 0.13f, 0.22f);
    public static readonly Color SectionHeader =
        new Color(0.25f, 0.15f, 0.085f, 0.70f);
    public static readonly Color PanelInset = new Color(0.032f, 0.028f, 0.025f, 1f);
    public static readonly Color Card = Panel;
    public static readonly Color CardSoft = PanelTop;
    public static readonly Color CardHover = new Color(0.145f, 0.100f, 0.070f, 1f);
    public static readonly Color CardHeader = PanelHeader;
    public static readonly Color CardInset = PanelInset;
    public static readonly Color ItemHeader = new Color(0.090f, 0.068f, 0.052f, 1f);
    public static readonly Color ItemHeaderHover = new Color(0.150f, 0.100f, 0.065f, 1f);
    public static readonly Color ItemBody = new Color(0.047f, 0.041f, 0.036f, 1f);
    public static readonly Color ItemBadge = new Color(0.180f, 0.145f, 0.105f, 1f);
    public static readonly Color ItemBadgeBorder = new Color(0.66f, 0.48f, 0.28f, 0.24f);
    public static readonly Color HierarchyRowAlternate =
        new Color(0.095f, 0.078f, 0.064f, 1f);
    public static readonly Color TreeDropTarget =
        new Color(0.25f, 0.48f, 0.39f, 0.72f);
    public static readonly Color TreeRowHover =
        new Color(0.145f, 0.105f, 0.070f, 0.94f);
    public static readonly Color TreeRowEven =
        new Color(0.070f, 0.058f, 0.050f, 0.86f);
    public static readonly Color TreeRowOdd =
        new Color(0.057f, 0.051f, 0.045f, 0.84f);
    public static readonly Color TreeRequiredAccent =
        new Color(0.42f, 0.72f, 0.66f, 0.82f);
    public static readonly Color TreeCustomAccent =
        new Color(0.90f, 0.50f, 0.22f, 0.82f);
    public static readonly Color TreeGrip =
        new Color(0.56f, 0.60f, 0.65f, 0.9f);
    public static readonly Color TreeGripHover =
        new Color(0.82f, 0.86f, 0.9f, 0.95f);
    public static readonly Color TreeArrow =
        new Color(0.72f, 0.75f, 0.79f, 0.95f);
    public static readonly Color TreeInsertion =
        new Color(0.98f, 0.67f, 0.28f, 0.98f);
    public static readonly Color Field = new Color(0.070f, 0.056f, 0.048f, 1f);
    public static readonly Color FieldHover = new Color(0.100f, 0.076f, 0.060f, 1f);
    public static readonly Color FieldFocus = new Color(0.110f, 0.070f, 0.045f, 1f);
    public static readonly Color FieldBorder = new Color(0.62f, 0.48f, 0.34f, 0.42f);
    public static readonly Color FieldBorderHover = new Color(0.92f, 0.56f, 0.25f, 0.62f);
    public static readonly Color FieldBorderFocus = new Color(1.00f, 0.70f, 0.34f, 0.90f);
    public static readonly Color ToggleOff = new Color(0.125f, 0.100f, 0.085f, 1f);
    public static readonly Color ToggleOffHover = new Color(0.175f, 0.125f, 0.095f, 1f);
    public static readonly Color ToggleOn = new Color(0.68f, 0.31f, 0.12f, 1f);
    public static readonly Color ToggleOnHover = new Color(0.86f, 0.44f, 0.16f, 1f);
    public static readonly Color ToggleThumbOff = new Color(0.56f, 0.50f, 0.45f, 1f);
    public static readonly Color ToggleThumbOn = new Color(1.00f, 0.93f, 0.78f, 1f);
    public static readonly Color ButtonPrimaryLeft = new Color(0.76f, 0.35f, 0.13f, 1f);
    public static readonly Color ButtonPrimaryRight = new Color(0.265f, 0.105f, 0.045f, 1f);
    public static readonly Color ButtonSecondaryLeft = new Color(0.235f, 0.175f, 0.125f, 1f);
    public static readonly Color ButtonSecondaryRight = new Color(0.078f, 0.065f, 0.057f, 1f);
    public static readonly Color ButtonDangerLeft = new Color(0.540f, 0.100f, 0.055f, 1f);
    public static readonly Color ButtonDangerRight = new Color(0.170f, 0.050f, 0.045f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.100f, 0.085f, 0.075f, 1f);
    public static readonly Color ButtonDisabledText = new Color(0.50f, 0.45f, 0.41f, 1f);
    public static readonly Color Border = new Color(0.58f, 0.47f, 0.38f, 0.20f);
    public static readonly Color BorderSoft = new Color(0.58f, 0.47f, 0.38f, 0.07f);
    public static readonly Color BorderStrong = new Color(0.70f, 0.54f, 0.40f, 0.34f);
    public static readonly Color BorderDark = new Color(0.018f, 0.016f, 0.014f, 0.90f);
    public static readonly Color FloatingDock =
        new Color(0.047f, 0.040f, 0.035f, 1f);
    public static readonly Color FloatingDockBorder =
        new Color(0.62f, 0.48f, 0.34f, 0.24f);
    public static readonly Color FloatingDockAccent = BorderStrong;
    public static readonly Color Text = new Color(0.975f, 0.935f, 0.865f, 1f);
    public static readonly Color TextMuted = new Color(0.790f, 0.715f, 0.630f, 1f);
    public static readonly Color TextDim = new Color(0.555f, 0.485f, 0.420f, 1f);
    public static readonly Color Value = new Color(0.88f, 0.82f, 0.74f, 1f);
    public static readonly Color InfoBar = new Color(0.050f, 0.072f, 0.065f, 1f);
    public static readonly Color InfoAccent = new Color(0.38f, 0.64f, 0.56f, 1f);
    public static readonly Color Warning = new Color(1.00f, 0.78f, 0.24f, 1f);
    public static readonly Color Error = new Color(1.00f, 0.28f, 0.18f, 1f);
    public static readonly Color Success = new Color(0.48f, 0.82f, 0.46f, 1f);
    public const float ControlRowMinimumHeight = 32f;
    public const float ControlHeight = 28f;
    public const float ControlVerticalMargin = 2f;
    public const float FieldLabelMinimumWidth = 112f;
    public const float FieldLabelWidthPercent = 36f;
    public const float FieldLabelMaximumWidth = 260f;
    public const float FieldInputMinimumWidth = 0f;
    public const float FieldStackBreakpoint = 360f;
    public const float ToggleWrapBreakpoint = 300f;
    public const float CompactLayoutBreakpoint = 500f;
    public const float ToggleWidth = 42f;
    public const float ToggleHeight = 20f;

    /// <summary>Returns the canonical surface treatment for a semantic role.</summary>
    public static ThreadlightEditorPalette Palette(ThreadlightEditorTone tone) {
        switch (tone) {
            case ThreadlightEditorTone.Standard:
                return new ThreadlightEditorPalette(ModuleStandard, ModuleStandardHover,
                    AuroraMagenta, Border);
            case ThreadlightEditorTone.Core:
                return new ThreadlightEditorPalette(ModuleCore, ModuleCoreHover,
                    AccentBright, BorderStrong);
            case ThreadlightEditorTone.Feature:
                return new ThreadlightEditorPalette(ModuleInteractive, ModuleInteractiveHover,
                    AuroraMagenta, BorderStrong);
            case ThreadlightEditorTone.External:
                return new ThreadlightEditorPalette(ModuleIntegration, ModuleIntegrationHover,
                    AuroraCyan, WithAlpha(AuroraCyan, .58f));
            case ThreadlightEditorTone.Project:
                return new ThreadlightEditorPalette(ModuleProject, ModuleProjectHover,
                    ProjectAccent, WithAlpha(ProjectAccent, .56f));
            case ThreadlightEditorTone.Folder:
                return new ThreadlightEditorPalette(
                    Color.Lerp(PanelInset, WorkspaceFolderAccent, .12f),
                    Color.Lerp(PanelTop, WorkspaceFolderAccent, .18f),
                    WorkspaceFolderAccent, WithAlpha(WorkspaceFolderAccent, .52f));
            case ThreadlightEditorTone.Export:
                return WorkspacePalette(WorkspaceExportAccent);
            case ThreadlightEditorTone.Review:
                return WorkspacePalette(WorkspaceReviewAccent);
            default:
                return new ThreadlightEditorPalette(ModuleStandard, ModuleStandardHover,
                    AuroraMagenta, Border);
        }
    }

    private static ThreadlightEditorPalette WorkspacePalette(Color accent) =>
        new ThreadlightEditorPalette(
            Color.Lerp(PanelInset, accent, .10f),
            Color.Lerp(PanelTop, accent, .16f),
            accent,
            WithAlpha(accent, .52f));

    public static Color WithAlpha(Color color, float alpha) =>
        new Color(color.r, color.g, color.b, alpha);
}
}
