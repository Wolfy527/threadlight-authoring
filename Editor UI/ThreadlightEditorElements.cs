namespace Threadlight.EditorUI {
using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
public static partial class ThreadlightEditorElements {
    private const string StyleGuid = "e4d32af3dc8c4fe2916ba01a6f5991d3";
    private const string StylePath = "Packages/com.wolfyvr.threadlight.authoring/Editor UI/ThreadlightEditorElements.uss";
    private static Texture2D auroraTexture, headerLogo;
    private static readonly ConditionalWeakTable<Button, ButtonVisualBinding> buttonVisualBindings =
        new ConditionalWeakTable<Button, ButtonVisualBinding>();
    private static bool headerLogoResolved;
    private static int headerLogoResolutionCount;
    private static StyleSheet styles;
    static ThreadlightEditorElements() {
        EditorApplication.projectChanged -= InvalidateHeaderLogoCache;
        EditorApplication.projectChanged += InvalidateHeaderLogoCache;
    }
    internal static int HeaderLogoResolutionCount => headerLogoResolutionCount;
    internal static void InvalidateHeaderLogoCache() { headerLogo = null; headerLogoResolved = false; }
    public static void ApplySharedStyles(VisualElement root) {
        StyleSheet sheet = SharedStyleSheet();
        if (root != null && sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
    }
    private static VisualElement Element(string className) {
        VisualElement element = new VisualElement();
        ApplySharedStyles(element);
        element.AddToClassList(className);
        return element;
    }
    private static Label Text(string value, string className, Color color) {
        Label label = new Label(value ?? string.Empty);
        label.AddToClassList(className);
        label.style.color = color;
        return label;
    }
    private static void AddCopy(VisualElement parent, string title, string body, string titleClass, string bodyClass) {
        parent.Add(Text(title, titleClass, ThreadlightEditorTheme.Text));
        if (!string.IsNullOrWhiteSpace(body)) parent.Add(Text(body.Trim(), bodyClass, ThreadlightEditorTheme.TextMuted));
    }
    public static VisualElement CreateInspectorRoot() {
        VisualElement root = Element("threadlight-inspector-root");
        BindMotionPreference(root);
        root.style.backgroundColor = ThreadlightEditorTheme.Background;
        root.Add(CreateAuroraAtmosphere(root));
        return root;
    }
    public static VisualElement CreateAuroraAtmosphere(VisualElement motionOwner) {
        VisualElement atmosphere = Element("threadlight-aurora-atmosphere"); atmosphere.pickingMode = PickingMode.Ignore;
        VisualElement[] layers = { Glow(-90, -170, 540, 380, ThreadlightEditorTheme.AuroraViolet, .28f),
            Glow(-130, -160, 440, 420, ThreadlightEditorTheme.AuroraCyan, .21f, true, true),
            Glow(45, 115, 330, 270, ThreadlightEditorTheme.AuroraMagenta, .16f, true) };
        foreach (VisualElement layer in layers) atmosphere.Add(layer); BindAuroraMotion(motionOwner, layers);
        return atmosphere;
    }
    public static VisualElement CreateInspectorBanner(string title, string description) =>
        CreateInspectorBanner(title, description, ThreadlightEditorTheme.AuroraViolet);
    public static VisualElement CreateInspectorBanner(string title, string description, Color accent) {
        VisualElement banner = Element("threadlight-banner");
        banner.AddToClassList("threadlight-level--page");
        banner.style.backgroundColor = Color.Lerp(ThreadlightEditorTheme.ModuleCore, ThreadlightEditorTheme.HeaderRight, .18f);
        SetBorderColor(banner, Color.Lerp(ThreadlightEditorTheme.BorderStrong, accent, .38f));
        Texture2D logo = LoadHeaderLogo();
        if (logo != null) {
            Image image = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("threadlight-banner-logo");
            StyleWordmark(image, 112f, 34f);
            banner.Add(image);
        }
        VisualElement copy = Element("threadlight-banner-copy");
        AddCopy(copy, title, description, "threadlight-banner-title", "threadlight-banner-description");
        banner.Add(copy);
        RegisterPointerLight(banner, accent, 138, .46f);
        return banner;
    }
    public static VisualElement CreateMessage(string title, string message, MessageType type = MessageType.Info) {
        Color accent = type == MessageType.Error ?
            ThreadlightEditorTheme.Error : ThreadlightEditorTheme.Warning;
        float fillStrength = type == MessageType.Error ? .19f : .15f;
        float hoverStrength = type == MessageType.Error ? .27f : .22f;
        float borderAlpha = type == MessageType.Error ? .84f : .72f;
        Color rest = Color.Lerp(ThreadlightEditorTheme.PanelInset, accent, fillStrength);
        Color hover = Color.Lerp(ThreadlightEditorTheme.PanelInset, accent, hoverStrength);
        Color border = ThreadlightEditorTheme.WithAlpha(accent, borderAlpha);
        Color hoverBorder = Color.Lerp(accent, Color.white, .24f);
        VisualElement card = Element("threadlight-message");
        card.AddToClassList(type == MessageType.Error ? "threadlight-message--error" :
            type == MessageType.Warning ? "threadlight-message--warning" :
            "threadlight-message--info");
        AddCopy(card, title, message, "threadlight-message-title", "threadlight-message-body");
        Label titleLabel = card.Q<Label>(className: "threadlight-message-title");
        if (titleLabel != null)
            titleLabel.style.color = accent;
        RegisterSurfaceInteraction(card,
            () => rest,
            () => hover,
            () => border,
            () => hoverBorder);
        RegisterPointerLight(card, accent, 104, .36f);
        return card;
    }
    public static void StyleFolderCardSurface(VisualElement element, Color accent, Color background, float radius = 8f) {
        if (element == null) return;
        ApplySharedStyles(element);
        element.AddToClassList("threadlight-bordered-surface");
        element.style.backgroundColor = background;
        // A folder frame is part of its resting hierarchy, not hover feedback.
        // Start from the normal border token so nested surfaces remain legible
        // before any pointer event, then let interaction only increase emphasis.
        Color border = Color.Lerp(ThreadlightEditorTheme.Border, accent, .28f);
        border.a = Mathf.Max(border.a, .40f);
        SetBorderColor(element, border);
        SetRadius(element, radius);
        element.MarkDirtyRepaint();
    }
    public static VisualElement CreatePageSection(string title, string description, Color accent, out VisualElement content) {
        VisualElement section = Element("threadlight-page-section");
        section.AddToClassList("threadlight-level--feature");
        StyleFolderCardSurface(section, accent, Color.Lerp(ThreadlightEditorTheme.PanelInset, ThreadlightEditorTheme.Panel, .34f));
        AddCopy(section, title, description, "threadlight-page-section-title", "threadlight-page-section-body");
        content = Element("threadlight-page-section-content");
        section.Add(content);
        RegisterPointerLight(section, accent, 122, .38f);
        return section;
    }
    public static VisualElement CreateReadOnlyValue(string label, string value) {
        VisualElement row = Element("threadlight-read-only-row");
        BindWidthClass(row, "threadlight-read-only-row--stacked", 280f);
        row.style.backgroundColor = ThreadlightEditorTheme.ItemBody;
        row.Add(Text(label, "threadlight-read-only-name", ThreadlightEditorTheme.TextMuted));
        row.Add(Text(value, "threadlight-read-only-value", ThreadlightEditorTheme.Value));
        return row;
    }
    /// <summary>Creates the shared wrapping action strip used below cards and field groups.</summary>
    public static VisualElement CreateActionStrip() {
        VisualElement row = Element("threadlight-action-strip");
        BindWidthClass(row, "threadlight-action-strip--stacked", 340f);
        return row;
    }
    /// <summary>Applies the shared responsive field-and-action row presentation.</summary>
    public static void StyleFieldActionRow(VisualElement row) {
        if (row == null) return;
        ApplySharedStyles(row);
        row.AddToClassList("threadlight-field-action-row");
        BindWidthClass(row, "threadlight-field-action-row--stacked", 340f);
    }
    public static Label CreateSubcategoryLabel(string text, Color? accent = null) {
        Label label = new Label(text ?? string.Empty);
        label.AddToClassList("threadlight-subcategory-label");
        label.AddToClassList("threadlight-level--detail");
        if (!accent.HasValue) return label;
        Color color = accent.Value;
        label.style.color = color;
        label.style.backgroundColor = new Color(color.r, color.g, color.b, .11f);
        return label;
    }
    /// <summary>Creates a compact semantic status badge with shared color treatment.</summary>
    public static Label CreateStatusBadge(string className = null) {
        Label badge = new Label();
        ApplySharedStyles(badge);
        badge.AddToClassList("threadlight-status-badge");
        if (!string.IsNullOrWhiteSpace(className))
            badge.AddToClassList(className);
        return badge;
    }
    public static void UpdateStatusBadge(
        Label badge,
        string text,
        Color accent,
        bool visible = true) {
        if (badge == null) return;
        badge.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;
        badge.text = text ?? string.Empty;
        badge.style.color = accent;
        badge.style.backgroundColor = Color.Lerp(
            ThreadlightEditorTheme.PanelInset, accent, .10f);
        SetBorderColor(badge, ThreadlightEditorTheme.WithAlpha(accent, .42f));
    }
    public static Button CreatePrimaryButton(string text, Action clicked, Color? accent = null) =>
        CreateButton(text, clicked, "threadlight-primary-button",
            accent.HasValue ? Color.Lerp(ThreadlightEditorTheme.PanelInset, accent.Value, .62f) :
                ThreadlightEditorTheme.ButtonPrimaryLeft,
            accent.HasValue ? ThreadlightEditorTheme.WithAlpha(accent.Value, .72f) :
                ThreadlightEditorTheme.BorderStrong, false, accent);
    public static Button CreateCompactButton(string text, Action clicked, bool danger = false, Color? accent = null) =>
        CreateButton(text, clicked, "threadlight-compact-button",
            danger ? ThreadlightEditorTheme.ButtonDangerLeft : accent.HasValue
                ? Color.Lerp(ThreadlightEditorTheme.PanelInset, accent.Value, .30f)
                : ThreadlightEditorTheme.ButtonSecondaryLeft,
            danger ? ThreadlightEditorTheme.Error : accent.HasValue
                ? ThreadlightEditorTheme.WithAlpha(accent.Value, .58f)
                : ThreadlightEditorTheme.FieldBorder, danger, accent);
    private static Button CreateButton(string text, Action clicked, string className, Color background, Color border,
        bool danger, Color? accent) {
        Button button = new Button(clicked) { text = text ?? string.Empty };
        ApplySharedStyles(button);
        button.AddToClassList(className);
        ClearDefaultToolkitButtonBackground(button);
        button.style.backgroundColor = background;
        button.style.color = ThreadlightEditorTheme.Text;
        SetBorderColor(button, border);
        Color interaction = danger ? ThreadlightEditorTheme.Error : accent ??
            ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Standard).Accent;
        RegisterButtonHover(button, background, danger
                ? Color.Lerp(background, interaction, .24f)
                : Color.Lerp(background, interaction, accent.HasValue ? .24f : .12f),
            border, Color.Lerp(interaction, Color.white, danger ? .28f : .22f));
        return button;
    }
    public static void StyleIconButton(Button button, Color background, Color border, Color text, Color hoverBackground) {
        StyleIconButton(button, () => background, () => border, text, () => hoverBackground,
            () => Color.Lerp(text, Color.white, .26f));
    }
    public static void StyleIconButton(Button button, Func<Color> background, Func<Color> border, Color text,
        Func<Color> hoverBackground, Func<Color> hoverBorder) {
        if (button == null) return;
        ApplySharedStyles(button);
        button.AddToClassList("threadlight-icon-button");
        ClearDefaultToolkitButtonBackground(button);
        button.style.color = text;
        RegisterButtonHover(button, background, hoverBackground, border, hoverBorder);
    }
    public static void SetButtonEnabled(Button button, bool enabled, bool primary = true) {
        if (button == null) return;
        button.SetEnabled(enabled);
        if (buttonVisualBindings.TryGetValue(button, out ButtonVisualBinding binding)) {
            binding.SetEnabled(enabled);
            return;
        }
        button.style.backgroundColor = enabled ? (primary ? ThreadlightEditorTheme.ButtonPrimaryLeft :
            ThreadlightEditorTheme.ButtonSecondaryLeft) : ThreadlightEditorTheme.ButtonDisabled;
        button.style.color = enabled ? ThreadlightEditorTheme.Text : ThreadlightEditorTheme.ButtonDisabledText;
        SetBorderColor(button, enabled ? ThreadlightEditorTheme.BorderStrong : ThreadlightEditorTheme.BorderSoft);
    }
    public static VisualElement CreateFooterDock(Color accent, float minimumHeight = 56f) {
        VisualElement dock = Element("threadlight-footer-dock");
        dock.style.minHeight = minimumHeight;
        StyleFooterDockSurface(dock, accent);
        return dock;
    }
    public static void StyleFooterDockSurface(VisualElement dock, Color accent) {
        if (dock == null) return;
        ApplySharedStyles(dock);
        dock.style.backgroundColor = ThreadlightEditorTheme.FloatingDock;
        SetBorderColor(dock, Color.Lerp(ThreadlightEditorTheme.FloatingDockBorder, accent, .42f));
        RegisterPointerLight(dock, accent, 118, .34f);
    }
    public static void BindWidthClass(VisualElement element, string className, float breakpoint) {
        if (element == null || string.IsNullOrWhiteSpace(className) || breakpoint <= 0f) return;
        bool? applied = null;
        element.RegisterCallback<GeometryChangedEvent>(evt => {
            float width = evt.newRect.width;
            if (width <= 0f || float.IsNaN(width) || float.IsInfinity(width)) return;
            bool next = width < breakpoint;
            if (applied == next) return;
            applied = next;
            element.EnableInClassList(className, next);
        });
    }
    public static VisualElement CreateFooterCopy(string title, string status) => CreateFooterCopy(title, status, out _);
    public static VisualElement CreateFooterCopy(string title, string status, out Label statusLabel) {
        VisualElement copy = Element("threadlight-footer-copy");
        copy.Add(Text(title, "threadlight-footer-title", ThreadlightEditorTheme.Text));
        statusLabel = Text(status, "threadlight-footer-status", ThreadlightEditorTheme.TextMuted);
        copy.Add(statusLabel);
        return copy;
    }
    public static VisualElement CreateCard(Color accent) {
        VisualElement card = Element("threadlight-card");
        card.AddToClassList("threadlight-level--section");
        StyleFolderCardSurface(card, accent, ThreadlightEditorTheme.Panel);
        RegisterPointerLight(card, accent, 104, .36f);
        return card;
    }
    public static VisualElement CreateCardCopy(string title, string description) {
        VisualElement copy = new VisualElement();
        AddCopy(copy, title, description, "threadlight-card-title", "threadlight-card-body");
        return copy;
    }
    public static void ClearDefaultToolkitButtonBackground(Button button) {
        if (button == null) return;
        button.style.backgroundImage = new StyleBackground();
        button.style.unityBackgroundImageTintColor = Color.clear;
    }
    public static void StyleOverlayScrollbar(ScrollView scrollView) => StyleOverlayScrollbar(scrollView, null);
    public static void StyleOverlayScrollbar(ScrollView scrollView, Color? accent) {
        if (scrollView?.verticalScroller == null) return;
        ApplySharedStyles(scrollView);
        Scroller scroller = scrollView.verticalScroller;
        if (scroller.parent != scrollView) { scroller.RemoveFromHierarchy(); scrollView.hierarchy.Add(scroller); }
        scrollView.contentViewport.AddToClassList("threadlight-overlay-viewport");
        scrollView.contentContainer.AddToClassList("threadlight-overlay-content");
        scroller.AddToClassList("threadlight-overlay-scroller");
        scroller.lowButton.AddToClassList("threadlight-overlay-scroller-button");
        scroller.highButton.AddToClassList("threadlight-overlay-scroller-button");
        scroller.slider.AddToClassList("threadlight-overlay-slider");
        VisualElement dragger = scroller.slider.Q<VisualElement>(className: "unity-base-slider__dragger");
        if (dragger == null) return;
        dragger.AddToClassList("threadlight-overlay-dragger");
        Color color = accent ?? ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Standard).Accent;
        dragger.style.backgroundColor = new Color(color.r, color.g, color.b, .48f);
    }
    private static VisualElement Glow(float x, float y, float width, float height, Color tint, float opacity,
        bool right = false, bool bottom = false) {
        VisualElement glow = new VisualElement { pickingMode = PickingMode.Ignore };
        glow.style.position = Position.Absolute; glow.style.width = width; glow.style.height = height;
        if (right) glow.style.right = x; else glow.style.left = x;
        if (bottom) glow.style.bottom = y; else glow.style.top = y;
        glow.style.backgroundImage = new StyleBackground(AuroraTexture());
        glow.style.unityBackgroundImageTintColor = tint; glow.style.opacity = opacity;
        return glow;
    }
    private static Texture2D AuroraTexture() => auroraTexture != null ? auroraTexture : auroraTexture =
        AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.wolfyvr.threadlight.authoring/Editor UI/Aurora Glow.png");
    private static Texture2D LoadHeaderLogo() {
        if (headerLogoResolved && (ReferenceEquals(headerLogo, null) || headerLogo != null)) return headerLogo;
        headerLogoResolutionCount++;
        string path = AssetDatabase.GUIDToAssetPath(ThreadlightEditorTheme.HeaderLogoGuid);
        headerLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(string.IsNullOrWhiteSpace(path) ? ThreadlightEditorTheme.HeaderLogoPath : path);
        headerLogoResolved = true;
        return headerLogo;
    }
    private static StyleSheet SharedStyleSheet() {
        if (styles != null) return styles;
        string path = AssetDatabase.GUIDToAssetPath(StyleGuid);
        return styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(string.IsNullOrWhiteSpace(path) ? StylePath : path);
    }
    public static void SetBorderColor(VisualElement element, Color color) {
        element.style.borderTopColor = element.style.borderRightColor = color;
        element.style.borderBottomColor = element.style.borderLeftColor = color;
    }
    private static void RegisterButtonHover(Button button, Color normal, Color hover, Color border, Color hoverBorder) {
        RegisterButtonHover(button, () => normal, () => hover, () => border, () => hoverBorder);
    }
    public static void RegisterButtonInteraction(Button button, Func<Color> normal, Func<Color> hover,
        Func<Color> border, Func<Color> hoverBorder) =>
        RegisterButtonHover(button, normal, hover, border, hoverBorder);
    private static void RegisterButtonHover(Button button, Func<Color> normal, Func<Color> hover,
        Func<Color> border, Func<Color> hoverBorder) {
        bool enabled = button.enabledSelf;
        Action reset = RegisterInteractionState(button, (hovered, focused) => {
            if (!enabled) {
                button.style.backgroundColor = ThreadlightEditorTheme.ButtonDisabled;
                button.style.color = ThreadlightEditorTheme.ButtonDisabledText;
                SetBorderColor(button, ThreadlightEditorTheme.BorderSoft);
                return;
            }
            Color interactionBorder = hoverBorder?.Invoke() ?? ThreadlightEditorTheme.FieldBorderHover;
            button.style.color = ThreadlightEditorTheme.Text;
            button.style.backgroundColor = hovered ? hover() : normal();
            SetBorderColor(button, focused ? Color.Lerp(interactionBorder, Color.white, .18f) :
                hovered ? interactionBorder : border());
        });
        buttonVisualBindings.Remove(button);
        buttonVisualBindings.Add(button, new ButtonVisualBinding(value => {
            enabled = value;
            reset();
        }));
    }

    private sealed class ButtonVisualBinding {
        private readonly Action<bool> setEnabled;
        internal ButtonVisualBinding(Action<bool> setEnabled) { this.setEnabled = setEnabled; }
        internal void SetEnabled(bool enabled) => setEnabled(enabled);
    }

    /// <summary>
    /// Owns the common hover/focus lifecycle for custom Toolkit controls and
    /// returns a reset action for transient controls that launch another window.
    /// </summary>
    public static Action RegisterInteractionState(VisualElement element, Action<bool, bool> apply) =>
        RegisterInteractionState(element, apply, out _, false);

    public static Action RegisterInteractionState(VisualElement element, Action<bool, bool> apply,
        bool includeDescendantFocus) =>
        RegisterInteractionState(element, apply, out _, includeDescendantFocus);

    /// <summary>
    /// Registers the shared interaction lifecycle and exposes a refresh action
    /// for controls whose semantic state can change while they remain hovered.
    /// </summary>
    public static Action RegisterInteractionState(VisualElement element, Action<bool, bool> apply,
        out Action refresh) => RegisterInteractionState(element, apply, out refresh, false);

    public static Action RegisterInteractionState(VisualElement element, Action<bool, bool> apply,
        out Action refresh, bool includeDescendantFocus) {
        refresh = () => { };
        if (element == null || apply == null) return () => { };
        if (element is Button) element.AddToClassList("threadlight-interactive-button");
        bool hovered = false, focused = false;
        void Refresh() => apply(hovered, focused);
        void Reset() { hovered = focused = false; Refresh(); }
        refresh = Refresh;
        Refresh();
        element.RegisterCallback<PointerEnterEvent>(evt => {
            if (evt.target != element) return;
            hovered = true; Refresh();
        });
        element.RegisterCallback<PointerLeaveEvent>(evt => {
            if (evt.target != element) return;
            hovered = false; Refresh();
        });
        element.RegisterCallback<FocusInEvent>(evt => {
            if (!includeDescendantFocus && evt.target != element) return;
            if (focused) return;
            focused = true; Refresh();
        });
        element.RegisterCallback<FocusOutEvent>(evt => {
            if (!includeDescendantFocus && evt.target != element) return;
            if (evt.relatedTarget is VisualElement next && element.Contains(next)) return;
            focused = false; Refresh();
        });
        element.RegisterCallback<DetachFromPanelEvent>(evt => {
            if (evt.target == element) Reset();
        });
        return Reset;
    }

    public static Action RegisterSurfaceInteraction(VisualElement element,
        Func<Color> background, Func<Color> hoverBackground,
        Func<Color> border, Func<Color> hoverBorder) =>
        RegisterSurfaceInteraction(element, background, hoverBackground, border, hoverBorder, out _);

    public static Action RegisterSurfaceInteraction(VisualElement element,
        Func<Color> background, Func<Color> hoverBackground,
        Func<Color> border, Func<Color> hoverBorder, out Action refresh) =>
        RegisterInteractionState(element, (hovered, focused) => {
            element.style.backgroundColor = hovered ? hoverBackground() : background();
            SetBorderColor(element, hovered || focused ? hoverBorder() : border());
        }, out refresh);
    public static void SetRadius(VisualElement element, float radius) {
        element.style.borderTopLeftRadius = element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
    }
}
}
