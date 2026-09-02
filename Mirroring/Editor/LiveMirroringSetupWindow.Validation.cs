namespace Threadlight.Mirroring.Editor {
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
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
        int errorCount = diagnostics.Errors;
        int warningCount = diagnostics.Warnings;
        if (validationSlots.TryGetValue(
                "@footer",
                out VisualElement footerValidation) &&
            (errorCount > 0 || warningCount > 0)) {
            footerValidation.Add(CreateValidationSummaryStrip(
                errorCount,
                warningCount,
                diagnostics.HasBlockingErrors));
        }
        setupCardValidation.Clear();
        for (int i = 0; i < diagnostics.Messages.Count; i++) {
            LiveMirroringValidationMessage message = diagnostics.Messages[i];
            string path = string.IsNullOrWhiteSpace(message.PropertyPath)
                ? "@footer"
                : message.PropertyPath;
            if (!validationSlots.TryGetValue(
                    path,
                    out VisualElement container)) {
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
    }
    private static VisualElement CreateValidationSummaryStrip(
        int errors,
        int warnings,
        bool blocked) {
        Color status = blocked
            ? ThreadlightEditorTheme.Error
            : ThreadlightEditorTheme.Warning;
        VisualElement strip = new VisualElement();
        strip.AddToClassList("threadlight-mirroring-validation-summary");
        strip.style.backgroundColor = new Color(
            status.r,
            status.g,
            status.b,
            0.14f);
        Color border = new Color(
            status.r,
            status.g,
            status.b,
            0.56f);
        ThreadlightEditorElements.SetBorderColor(strip, border);
        string warningText = warnings > 0
            ? $" · {warnings} warning{(warnings == 1 ? string.Empty : "s")}"
            : string.Empty;
        Label label = new Label(blocked
            ? $"Build blocked · {errors} error{(errors == 1 ? string.Empty : "s")}{warningText}"
            : $"Review recommended · {warnings} warning{(warnings == 1 ? string.Empty : "s")}");
        label.AddToClassList(
            "threadlight-mirroring-validation-summary-label");
        label.style.color = status;
        strip.Add(label);
        return strip;
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
}
}
