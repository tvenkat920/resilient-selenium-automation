using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;

namespace ResilientSeleniumAutomation.Core.Resilience
{
    /// <summary>
    /// Represents an element candidate discovered in the DOM alongside all extracted signals and computed score.
    /// </summary>
    public class CandidateElement
    {
        public IWebElement Element { get; }

        // Extracted Signals
        public string TagName { get; set; } = string.Empty;
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? ClassName { get; set; }
        public string? InputType { get; set; }
        public string? Placeholder { get; set; }
        public string? Value { get; set; }
        public string? Title { get; set; }
        public string? AriaLabel { get; set; }
        public string? AriaLabelledBy { get; set; }
        public string? FormId { get; set; }

        public string? AssociatedLabelText { get; set; }
        public string? WrappingLabelText { get; set; }
        public string? SiblingLabelText { get; set; }
        public string? ContainerContextText { get; set; }
        public string? RadioOptionLabelText { get; set; }

        public bool IsDisplayed { get; set; }
        public bool IsEnabled { get; set; }

        public List<string> SelectOptions { get; set; } = new();

        // Scoring Information
        public double Score { get; set; }
        public Dictionary<string, double> ScoreBreakdown { get; } = new();

        public CandidateElement(IWebElement element)
        {
            Element = element;
        }

        /// <summary>
        /// Concatenates all textual signals associated with this candidate for text matching.
        /// </summary>
        public IEnumerable<string> GetAllAssociatedTexts()
        {
            if (!string.IsNullOrWhiteSpace(AssociatedLabelText)) yield return AssociatedLabelText;
            if (!string.IsNullOrWhiteSpace(WrappingLabelText)) yield return WrappingLabelText;
            if (!string.IsNullOrWhiteSpace(SiblingLabelText)) yield return SiblingLabelText;
            if (!string.IsNullOrWhiteSpace(RadioOptionLabelText)) yield return RadioOptionLabelText;
            if (!string.IsNullOrWhiteSpace(AriaLabel)) yield return AriaLabel;
            if (!string.IsNullOrWhiteSpace(Title)) yield return Title;
            if (!string.IsNullOrWhiteSpace(Placeholder)) yield return Placeholder;
            if (!string.IsNullOrWhiteSpace(ContainerContextText)) yield return ContainerContextText;
        }

        public string GetIdentifierSummary()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Id)) parts.Add($"id='{Id}'");
            if (!string.IsNullOrEmpty(Name)) parts.Add($"name='{Name}'");
            if (!string.IsNullOrEmpty(InputType)) parts.Add($"type='{InputType}'");
            if (!string.IsNullOrEmpty(AssociatedLabelText)) parts.Add($"label='{AssociatedLabelText.Trim()}'");
            if (!string.IsNullOrEmpty(ContainerContextText)) parts.Add($"grp='{ContainerContextText.Trim()}'");
            if (!string.IsNullOrEmpty(RadioOptionLabelText)) parts.Add($"opt='{RadioOptionLabelText.Trim()}'");
            if (!string.IsNullOrEmpty(FormId)) parts.Add($"form='{FormId}'");
            if (!string.IsNullOrEmpty(Value)) parts.Add($"val='{Value}'");
            return string.Join(" ", parts);
        }

        public override string ToString()
        {
            return $"<{TagName} {GetIdentifierSummary()}> (Score: {Score:P1})";
        }
    }
}
