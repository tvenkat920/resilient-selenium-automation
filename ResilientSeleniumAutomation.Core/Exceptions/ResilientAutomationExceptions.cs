using System;

namespace ResilientSeleniumAutomation.Core.Exceptions
{
    /// <summary>
    /// Base exception for all resilient automation errors.
    /// </summary>
    public class ResilientAutomationException : Exception
    {
        public ResilientAutomationException(string message) : base(message) { }
        public ResilientAutomationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when no candidate element satisfies the minimum confidence threshold.
    /// Includes diagnostic evaluation summary.
    /// </summary>
    public class ElementNotFoundException : ResilientAutomationException
    {
        public string TargetSemantic { get; }
        public double RequiredThreshold { get; }
        public double HighestScoreAchieved { get; }
        public string DiagnosticSummary { get; }

        public ElementNotFoundException(string targetSemantic, double requiredThreshold, double highestScore, string diagnosticSummary)
            : base($"Unable to locate resilient element for target '{targetSemantic}'. Highest candidate score was {highestScore:P1}, which is below required threshold {requiredThreshold:P1}.\nDiagnostics:\n{diagnosticSummary}")
        {
            TargetSemantic = targetSemantic;
            RequiredThreshold = requiredThreshold;
            HighestScoreAchieved = highestScore;
            DiagnosticSummary = diagnosticSummary;
        }
    }

    /// <summary>
    /// Thrown when multiple candidate elements score above the confidence threshold
    /// with a score difference smaller than the ambiguity delta threshold.
    /// Prevents entering data or clicking the wrong field in production.
    /// </summary>
    public class AmbiguousElementMatchException : ResilientAutomationException
    {
        public string TargetSemantic { get; }
        public double ScoreCandidate1 { get; }
        public string DescriptionCandidate1 { get; }
        public double ScoreCandidate2 { get; }
        public string DescriptionCandidate2 { get; }
        public double Delta { get; }

        public AmbiguousElementMatchException(
            string targetSemantic,
            double score1,
            string desc1,
            double score2,
            string desc2,
            double delta,
            string diagnosticReport)
            : base($"Ambiguous match detected for target '{targetSemantic}'. Candidate #1 (Score: {score1:P1}, Element: {desc1}) and Candidate #2 (Score: {score2:P1}, Element: {desc2}) differ by {delta:P2}, which is within ambiguity threshold. Interaction aborted safely.\nDiagnostics:\n{diagnosticReport}")
        {
            TargetSemantic = targetSemantic;
            ScoreCandidate1 = score1;
            DescriptionCandidate1 = desc1;
            ScoreCandidate2 = score2;
            DescriptionCandidate2 = desc2;
            Delta = delta;
        }
    }

    /// <summary>
    /// Thrown when a dropdown interaction is attempted on an element that is not a native HTML &lt;select&gt;.
    /// Accurately reports unsupported custom dropdowns (e.g., div/ul bootstrap dropdowns) with actionable details.
    /// </summary>
    public class UnsupportedDropdownException : ResilientAutomationException
    {
        public string ActualTagName { get; }
        public string ElementDetails { get; }

        public UnsupportedDropdownException(string actualTagName, string elementDetails, string message)
            : base($"Unsupported dropdown component: Found <{actualTagName}> instead of native HTML <select>. Element: {elementDetails}. {message}")
        {
            ActualTagName = actualTagName;
            ElementDetails = elementDetails;
        }
    }
}

