namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private enum DiagnosticNavigationKind {
        ExactSetting,
        SectionAnchor
    }
    private struct ValidationCounts {
        public int Errors;
        public int Warnings;
        public void Add(LiveMirroringValidationSeverity severity) {
            if (severity == LiveMirroringValidationSeverity.Error)
                Errors++;
            else if (severity == LiveMirroringValidationSeverity.Warning)
                Warnings++;
        }
    }
    private VisualElement CreateValidationSlot(string propertyPath) {
        VisualElement slot = new VisualElement();
        validationSlots[propertyPath] = slot;
        return slot;
    }
    private void RebuildValidation() {
        foreach (VisualElement slot in validationSlots.Values)
            slot.Clear();
        workspace?.Query<LiveMirroringSetupCard>().ForEach(card =>
            card.SetValidationState(0, 0));
        workspace?.Query<LiveMirroringTargetCard>().ForEach(card =>
            card.SetValidationState(0, 0));
        diagnostics.CollectReport(serializedSystem);
        RefreshBuildChecker();
        setupCardValidation.Clear();
        for (int i = 0; i < diagnostics.Messages.Count; i++) {
            LiveMirroringValidationMessage message = diagnostics.Messages[i];
            string path = string.IsNullOrWhiteSpace(message.PropertyPath)
                ? "@footer"
                : message.PropertyPath;
            if (!validationSlots.TryGetValue(
                    path,
                    out VisualElement container)) {
                container = PairValidationSlot(message);
                if (container == null)
                    validationSlots.TryGetValue("@footer", out container);
            }
            container?.Add(ThreadlightEditorElements.CreateMessage(
                message.Title,
                message.Message,
                ToMessageType(message.Severity)
            ));
            if (message.Severity == LiveMirroringValidationSeverity.Info) {
                continue;
            }
            AddCardValidation(container, message.Severity);
        }
        foreach (KeyValuePair<LiveMirroringSetupCard, ValidationCounts> entry
                 in setupCardValidation) {
            entry.Key.SetValidationState(
                entry.Value.Errors,
                entry.Value.Warnings);
        }
        RefreshPairPresentation();
    }
    private Label checkerSummary, checkerArrow;
    private VisualElement checkerDot;
    private ScrollView checkerDetails;
    private VisualElement checkerReveal;
    private bool checkerExpanded;
    private string checkerUnavailable;
    private void AddBuildChecker(bool managed, bool supported, bool creating) {
        checkerUnavailable = managed ? "Build from the owning Builder."
            : !supported ? "Update ThreadLight Authoring before building."
            : creating ? candidateRoot == null ? "Choose a Prefab Root first."
                : EditorUtility.IsPersistent(candidateRoot) ? "Open the prefab in Prefab Mode first."
                : LiveMirroringSetupUtility.IsRootManagedByAnotherTool(candidateRoot, out _) ? "Create the setup from the owning Builder."
                : LiveMirroringSetupUtility.FindForRoot(candidateRoot).Length > 0 ? "Choose the existing setup above."
                : "Ready to create the setup and targets." : null;
        checkerExpanded = ThreadlightEditorPreferences.GetSessionState(
            "mirroring.window", null, "build-checker", checkerExpanded);
        VisualElement group = CheckerElement<VisualElement>("validation");
        Button toggle = new Button(() => {
            checkerExpanded = !checkerExpanded;
            ThreadlightEditorPreferences.SetSessionState(
                "mirroring.window", null, "build-checker", checkerExpanded);
            ThreadlightEditorElements.SetModuleGroupBodyExpansion(checkerReveal, checkerExpanded, true);
            checkerArrow.text = checkerExpanded ? "−" : "+";
        });
        toggle.AddToClassList("threadlight-checker-validation-strip");
        toggle.AddToClassList("threadlight-checker-validation-strip--readiness");
        ThreadlightEditorElements.ClearDefaultToolkitButtonBackground(toggle);
        ThreadlightEditorElements.StyleFolderCardSurface(toggle,
            ThreadlightEditorTheme.WorkspaceReviewAccent, ThreadlightEditorTheme.PanelInset, 7f);
        AddTooltip(toggle, "Build Checker", "Reviews this setup's errors, warnings, and build readiness.");
        toggle.Add(checkerDot = CheckerElement<VisualElement>("validation-dot"));
        Label title = CheckerElement<Label>("validation-title");
        title.text = "Build Checker";
        toggle.Add(title);
        checkerSummary = CheckerElement<Label>("validation-summary");
        checkerSummary.style.flexGrow = 1;
        toggle.Add(checkerSummary);
        checkerArrow = CheckerElement<Label>("validation-arrow");
        checkerArrow.text = checkerExpanded ? "−" : "+";
        toggle.Add(checkerArrow);
        group.Add(toggle);
        checkerDetails = new ScrollView(ScrollViewMode.Vertical);
        checkerDetails.AddToClassList("threadlight-checker-validation-messages");
        checkerDetails.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        ThreadlightEditorElements.StyleOverlayScrollbar(checkerDetails, ThreadlightEditorTheme.WorkspaceReviewAccent);
        checkerReveal = new VisualElement();
        checkerReveal.Add(checkerDetails);
        ThreadlightEditorElements.SetModuleGroupBodyExpansion(checkerReveal, checkerExpanded, false);
        group.Add(checkerReveal);
        footer.Add(group);
        RefreshBuildChecker(false);
    }
    private static T CheckerElement<T>(string suffix) where T : VisualElement, new() {
        T element = new T();
        element.AddToClassList("threadlight-checker-" + suffix);
        return element;
    }
    private void RefreshBuildChecker(bool hasReport = true) {
        if (checkerDetails == null) return;
        checkerDetails.Clear();
        bool available = checkerUnavailable == null && hasReport;
        int errors = available ? diagnostics.Errors : 0;
        int warnings = available ? diagnostics.Warnings : 0;
        string status = checkerUnavailable ?? (!hasReport ? "Checking Setup…"
            : errors > 0
                ? warnings > 0
                    ? $"Build Blocked · {FormatCount(errors, "error")} · {FormatCount(warnings, "warning")}"
                    : $"Build Blocked · {FormatCount(errors, "error")}"
            : warnings > 0 ? $"Ready with Warnings · {FormatCount(warnings, "warning")}" : "Ready to Build");
        Color color = errors > 0 ? ThreadlightEditorTheme.Error
            : warnings > 0 ? ThreadlightEditorTheme.Warning
            : available ? ThreadlightEditorTheme.Success : ThreadlightEditorTheme.TextMuted;
        checkerSummary.text = status;
        checkerSummary.style.color = color;
        checkerDot.style.backgroundColor = color;
        if (available && diagnostics.Messages.Count > 0) {
            foreach (LiveMirroringValidationMessage message in diagnostics.Messages)
                checkerDetails.Add(CreateCheckerMessage(message));
        } else {
            checkerDetails.Add(ThreadlightEditorElements.CreateMessage("Build Readiness", status, MessageType.Info));
        }
    }
    private static string FormatCount(int count, string singular) =>
        $"{count} {singular}{(count == 1 ? string.Empty : "s")}";
    private VisualElement CreateCheckerMessage(LiveMirroringValidationMessage message) {
        VisualElement row = ThreadlightEditorElements.CreateMessage(
            message.Title, message.Message, ToMessageType(message.Severity));
        if (TryResolveNavigationTarget(message, out _, out LiveMirroringSetupCard card,
                out DiagnosticNavigationKind kind)) {
            row.name = "threadlight-mirroring-navigable-diagnostic";
            row.userData = message.PropertyPath ?? (object)message;
            row.AddToClassList("threadlight-mirroring-diagnostic--navigable");
            ThreadlightEditorElements.RegisterNavigationActivation(
                row, () => NavigateToDiagnostic(message));
        }
        return row;
    }
    private void RefreshPairPresentation() {
        if (currentSystem == null || pairSummary == null)
            return;
        LiveMirroringEvaluationBuffers graph = LiveMirroringService.AnalyzePairs(currentSystem);
        int attention = 0;
        for (int index = 0; index < graph.PairFacts.Count; index++) {
            LiveMirroringPairFact fact = graph.PairFacts[index];
            string path = TargetCardPath(fact.Index);
            if (!targetCards.TryGetValue(path, out LiveMirroringTargetCard card))
                continue;
            setupCardValidation.TryGetValue(card, out ValidationCounts counts);
            bool needsAttention = counts.Errors > 0 || counts.Warnings > 0;
            if (needsAttention)
                attention++;
            card.SetPairState(fact.Status, counts.Errors, counts.Warnings);
            card.style.display = !showProblemPairsOnly || needsAttention
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
        int total = graph.PairFacts.Count;
        int ready = total - attention;
        string attentionText = attention == 1
            ? "1 needs attention"
            : $"{attention} need attention";
        pairSummary.text = $"{total} target{(total == 1 ? "" : "s")} · {ready} ready · {attentionText}";
        if (pairFilterEmptyState != null)
            pairFilterEmptyState.style.display = showProblemPairsOnly && total > 0 && attention == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        UpdatePairFilterButtons();
    }
    private void SetPairFilter(bool problemsOnly) {
        showProblemPairsOnly = problemsOnly;
        ThreadlightEditorPreferences.SetSessionState(
            PairFilterSurfaceId, currentSystem, "needs-attention", problemsOnly);
        RefreshPairPresentation();
    }
    private void UpdatePairFilterButtons() {
        if (showAllPairsButton != null)
            ThreadlightEditorElements.SetButtonEnabled(
                showAllPairsButton, showProblemPairsOnly, false);
        if (showProblemPairsButton != null)
            ThreadlightEditorElements.SetButtonEnabled(
                showProblemPairsButton, !showProblemPairsOnly, false);
    }
    private bool TryResolveNavigationTarget(
        LiveMirroringValidationMessage message,
        out VisualElement target,
        out LiveMirroringSetupCard card,
        out DiagnosticNavigationKind kind) {
        target = null;
        card = null;
        kind = DiagnosticNavigationKind.SectionAnchor;
        string propertyPath = message?.PropertyPath;
        if (string.IsNullOrWhiteSpace(propertyPath))
            return TryResolveExplicitPairAnchor(message, out target, out card);
        if (propertyNavigationTargets.TryGetValue(propertyPath, out target))
            card = target as LiveMirroringSetupCard ??
                target?.GetFirstAncestorOfType<LiveMirroringSetupCard>();
        if (target != null) {
            kind = DiagnosticNavigationKind.ExactSetting;
            return true;
        }
        if (validationSlots.TryGetValue(propertyPath, out target) && target != null) {
            card = target.GetFirstAncestorOfType<LiveMirroringSetupCard>();
            return true;
        }
        return TryResolveExplicitPairAnchor(message, out target, out card);
    }
    private bool TryResolveExplicitPairAnchor(
        LiveMirroringValidationMessage message,
        out VisualElement target,
        out LiveMirroringSetupCard card) {
        target = null;
        card = null;
        if (message == null || message.PairIndex < 0)
            return false;
        string pairPath = TargetCardPath(message.PairIndex);
        if (!targetCards.TryGetValue(pairPath, out LiveMirroringTargetCard pairCard))
            return false;
        target = card = pairCard;
        return true;
    }
    private VisualElement PairValidationSlot(LiveMirroringValidationMessage message) {
        if (TryResolveNavigationTarget(message, out _, out LiveMirroringSetupCard card,
                out _) && card is LiveMirroringTargetCard) {
            foreach (KeyValuePair<string, LiveMirroringTargetCard> entry in targetCards)
                if (ReferenceEquals(entry.Value, card) &&
                    validationSlots.TryGetValue(entry.Key, out VisualElement knownSlot))
                    return knownSlot;
        }
        return null;
    }
    private bool TryResolveNavigationTarget(
        string propertyPath,
        out VisualElement target,
        out LiveMirroringSetupCard card,
        out DiagnosticNavigationKind kind) =>
        TryResolveNavigationTarget(
            new LiveMirroringValidationMessage(
                LiveMirroringValidationSeverity.Info, string.Empty, null, propertyPath),
            out target, out card, out kind);
    private void NavigateToDiagnostic(LiveMirroringValidationMessage message) {
        if (!TryResolveNavigationTarget(message, out VisualElement target,
                out LiveMirroringSetupCard card, out DiagnosticNavigationKind kind))
            return;
        if (showProblemPairsOnly && card is LiveMirroringTargetCard)
            SetPairFilter(false);
        card?.Reveal();
        VisualElement destination = kind == DiagnosticNavigationKind.ExactSetting
            ? target
            : card ?? target;
        if (kind == DiagnosticNavigationKind.ExactSetting)
            target.Focus();
        int landingGeneration = ++diagnosticLandingGeneration;
        DeferDiagnosticNavigation(() => {
            if (landingGeneration != diagnosticLandingGeneration)
                return;
            if (!ThreadlightEditorElements.ScrollDiagnosticIntoSafeBand(
                    workspaceScroll, destination, footer))
                return;
            diagnosticDestinationPulse.Start(
                rootVisualElement, destination,
                card?.InteractionAccent ?? ThreadlightEditorTheme.HighlightAccent,
                DeferDiagnosticNavigation,
                ThreadlightEditorPreferences.ReducedMotion);
        }, destination);
    }
    private void NavigateToProperty(string propertyPath) {
        NavigateToDiagnostic(new LiveMirroringValidationMessage(
            LiveMirroringValidationSeverity.Info, string.Empty, null, propertyPath));
    }
    private void AddCardValidation(
        VisualElement element,
        LiveMirroringValidationSeverity severity) {
        for (VisualElement current = element; current != null; current = current.parent) {
            if (current is LiveMirroringSetupCard card) {
                setupCardValidation.TryGetValue(card, out ValidationCounts counts);
                counts.Add(severity);
                setupCardValidation[card] = counts;
            }
        }
    }

    private sealed class DeferredDiagnosticAction {
        public Action Callback;
        public VisualElement Owner;
        public int Generation;
        public double DueTime;
        public long InsertionOrder;
    }
    private readonly List<DeferredDiagnosticAction> deferredDiagnosticActions =
        new List<DeferredDiagnosticAction>();
    private readonly ThreadlightDestinationPulse diagnosticDestinationPulse =
        new ThreadlightDestinationPulse();
    private bool diagnosticActionPumpRegistered;
    private int diagnosticActionGeneration;
    private int diagnosticLandingGeneration;
    private long nextDiagnosticActionOrder;
    private void DeferDiagnosticNavigation(
        Action action, VisualElement owner, int delayMilliseconds = 0) {
        if (action == null)
            return;
        deferredDiagnosticActions.Add(new DeferredDiagnosticAction {
            Callback = action,
            Owner = owner,
            Generation = diagnosticActionGeneration,
            DueTime = EditorApplication.timeSinceStartup +
                Mathf.Max(0, delayMilliseconds) / 1000d,
            InsertionOrder = nextDiagnosticActionOrder++
        });
        if (diagnosticActionPumpRegistered)
            return;
        diagnosticActionPumpRegistered = true;
        EditorApplication.update += PumpDiagnosticNavigation;
    }
    private void PumpDiagnosticNavigation() {
        PumpDiagnosticNavigationAt(EditorApplication.timeSinceStartup);
    }
    private void PumpDiagnosticNavigationAt(double now) {
        var due = new List<DeferredDiagnosticAction>();
        for (int index = deferredDiagnosticActions.Count - 1; index >= 0; index--) {
            DeferredDiagnosticAction pending = deferredDiagnosticActions[index];
            if (pending.Generation != diagnosticActionGeneration ||
                pending.Owner == null || pending.Owner.panel != rootVisualElement?.panel) {
                deferredDiagnosticActions.RemoveAt(index);
                continue;
            }
            if (pending.DueTime > now)
                continue;
            deferredDiagnosticActions.RemoveAt(index);
            due.Add(pending);
        }
        due.Sort((left, right) => {
            int timeOrder = left.DueTime.CompareTo(right.DueTime);
            return timeOrder != 0
                ? timeOrder
                : left.InsertionOrder.CompareTo(right.InsertionOrder);
        });
        foreach (DeferredDiagnosticAction pending in due) {
            if (pending.Generation != diagnosticActionGeneration)
                continue;
            try { pending.Callback(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        if (deferredDiagnosticActions.Count != 0)
            return;
        diagnosticActionPumpRegistered = false;
        EditorApplication.update -= PumpDiagnosticNavigation;
    }
    private void CancelDiagnosticNavigationPresentation() {
        diagnosticActionGeneration++;
        diagnosticLandingGeneration++;
        deferredDiagnosticActions.Clear();
        if (diagnosticActionPumpRegistered) {
            diagnosticActionPumpRegistered = false;
            EditorApplication.update -= PumpDiagnosticNavigation;
        }
        diagnosticDestinationPulse.Cancel();
    }
}
}
