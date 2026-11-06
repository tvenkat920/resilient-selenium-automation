using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ResilientSeleniumAutomation.Core.Exceptions;
using ResilientSeleniumAutomation.Core.Logging;

namespace ResilientSeleniumAutomation.Core.Resilience
{
    /// <summary>
    /// Multi-signal candidate scoring engine.
    /// Evaluates visibility, enabled state, semantic label association, control type,
    /// contextual containers, accessible names, placeholder, and option domain matching.
    /// Implements safe failure upon ambiguous matches.
    /// </summary>
    public class CandidateScorer
    {
        private readonly ITestLogger _logger;

        public CandidateScorer(ITestLogger? logger = null)
        {
            _logger = logger ?? new ConsoleTestLogger();
        }

        /// <summary>
        /// Computes the composite score for a single candidate element against target criteria.
        /// Returns score clamped between 0.0 and 1.0.
        /// </summary>
        public double ScoreCandidate(TargetElementCriteria criteria, CandidateElement candidate)
        {
            candidate.ScoreBreakdown.Clear();

            // 1. Visibility & Enabled State (Gatekeeper: 0.20)
            double visibilityScore = 0.0;
            if (candidate.IsDisplayed && candidate.IsEnabled)
            {
                visibilityScore = 0.20;
            }
            else if (candidate.IsDisplayed && !candidate.IsEnabled)
            {
                visibilityScore = 0.05;
            }
            else
            {
                // Non-displayed elements (e.g. hidden inputs, template slots) get 0
                visibilityScore = 0.0;
            }
            candidate.ScoreBreakdown["Visibility"] = visibilityScore;

            // If completely hidden, score remains 0 to prevent interacting with off-screen/template elements
            if (!candidate.IsDisplayed)
            {
                candidate.Score = 0.0;
                return 0.0;
            }

            // 2. Control Type & Semantic Tag (Weight: 0.20)
            double typeScore = 0.0;
            switch (criteria.ExpectedControlType)
            {
                case ControlType.TextInput:
                    if (candidate.TagName == "input" && (string.IsNullOrEmpty(candidate.InputType) || candidate.InputType is "text" or "email" or "search" or "tel" or "url"))
                        typeScore = 0.20;
                    else if (candidate.TagName == "textarea")
                        typeScore = 0.15;
                    break;

                case ControlType.RadioButtonOption:
                case ControlType.RadioButtonGroup:
                    if (candidate.TagName == "input" && candidate.InputType == "radio")
                        typeScore = 0.20;
                    break;

                case ControlType.DropdownSelect:
                    if (candidate.TagName == "select")
                        typeScore = 0.20;
                    break;

                case ControlType.Checkbox:
                    if (candidate.TagName == "input" && candidate.InputType == "checkbox")
                        typeScore = 0.20;
                    break;

                case ControlType.Button:
                    if (candidate.TagName is "button" or "a" || (candidate.TagName == "input" && candidate.InputType is "submit" or "button"))
                        typeScore = 0.20;
                    break;

                case ControlType.Any:
                    typeScore = 0.10;
                    break;
            }
            candidate.ScoreBreakdown["ControlType"] = typeScore;

            // 3. Explicit Associated Label (Weight: 0.25)
            double labelScore = 0.0;
            var explicitLabels = new[] { candidate.AssociatedLabelText, candidate.WrappingLabelText, candidate.SiblingLabelText }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            foreach (var lbl in explicitLabels)
            {
                var matchStrength = CalculateTextSimilarity(lbl, criteria.SemanticLabel, criteria.AlternativeLabels);
                if (matchStrength * 0.25 > labelScore)
                {
                    labelScore = matchStrength * 0.25;
                }
            }
            candidate.ScoreBreakdown["ExplicitLabel"] = labelScore;

            // 4. Radio Option Label & Group Context (Weight: 0.25)
            double optionAndGroupScore = 0.0;
            if (criteria.ExpectedControlType == ControlType.RadioButtonOption && !string.IsNullOrEmpty(criteria.OptionValue))
            {
                // Score the option value match (e.g. "Male", "Female") - Weight 0.15
                double optMatch = 0.0;
                if (!string.IsNullOrEmpty(candidate.RadioOptionLabelText))
                {
                    optMatch = Math.Max(optMatch, CalculateTextSimilarity(candidate.RadioOptionLabelText, criteria.OptionValue, Enumerable.Empty<string>()));
                }
                if (!string.IsNullOrEmpty(candidate.AssociatedLabelText))
                {
                    optMatch = Math.Max(optMatch, CalculateTextSimilarity(candidate.AssociatedLabelText, criteria.OptionValue, Enumerable.Empty<string>()));
                }
                if (!string.IsNullOrEmpty(candidate.Value))
                {
                    optMatch = Math.Max(optMatch, CalculateTextSimilarity(candidate.Value, criteria.OptionValue, Enumerable.Empty<string>()));
                }
                if (!string.IsNullOrEmpty(candidate.Id))
                {
                    optMatch = Math.Max(optMatch, CalculateTextSimilarity(candidate.Id, criteria.OptionValue, Enumerable.Empty<string>()));
                }

                // Score the group label match in ancestor context (e.g. <label class="control-label">Gender</label>) - Weight 0.10
                double grpMatch = 0.0;
                if (!string.IsNullOrEmpty(candidate.ContainerContextText))
                {
                    grpMatch = CalculateTextSimilarity(candidate.ContainerContextText, criteria.SemanticLabel, criteria.AlternativeLabels);
                }
                else if (!string.IsNullOrEmpty(candidate.SiblingLabelText))
                {
                    grpMatch = CalculateTextSimilarity(candidate.SiblingLabelText, criteria.SemanticLabel, criteria.AlternativeLabels) * 0.7;
                }

                optionAndGroupScore = (optMatch * 0.15) + (grpMatch * 0.10);
            }
            else if (!string.IsNullOrEmpty(candidate.ContainerContextText))
            {
                // For non-radio, nearby container text matching semantic label
                var containerMatch = CalculateTextSimilarity(candidate.ContainerContextText, criteria.SemanticLabel, criteria.AlternativeLabels);
                optionAndGroupScore = containerMatch * 0.10;
            }
            candidate.ScoreBreakdown["ContextAndOption"] = optionAndGroupScore;

            // 5. Accessible Name & Placeholder (Weight: 0.15)
            double a11yScore = 0.0;
            if (!string.IsNullOrEmpty(candidate.AriaLabel))
            {
                a11yScore = Math.Max(a11yScore, CalculateTextSimilarity(candidate.AriaLabel, criteria.SemanticLabel, criteria.AlternativeLabels) * 0.15);
            }
            if (!string.IsNullOrEmpty(candidate.Title))
            {
                a11yScore = Math.Max(a11yScore, CalculateTextSimilarity(candidate.Title, criteria.SemanticLabel, criteria.AlternativeLabels) * 0.10);
            }
            if (!string.IsNullOrEmpty(candidate.Placeholder))
            {
                a11yScore = Math.Max(a11yScore, CalculateTextSimilarity(candidate.Placeholder, criteria.SemanticLabel, criteria.AlternativeLabels) * 0.10);
            }
            candidate.ScoreBreakdown["A11yAndPlaceholder"] = a11yScore;

            // 6. Name and Id Attribute Semantic Tokens (Weight: 0.10)
            double attributeTokenScore = 0.0;
            var attrTexts = new[] { candidate.Id, candidate.Name }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            foreach (var attr in attrTexts)
            {
                var attrSim = CalculateTextSimilarity(attr, criteria.SemanticLabel, criteria.AlternativeLabels);
                if (attrSim * 0.10 > attributeTokenScore)
                {
                    attributeTokenScore = attrSim * 0.10;
                }
            }
            candidate.ScoreBreakdown["AttributeTokens"] = attributeTokenScore;

            // 7. Dropdown Option Domain Match (Weight: 0.10)
            double domainScore = 0.0;
            if (criteria.ExpectedControlType == ControlType.DropdownSelect && candidate.SelectOptions.Count > 0)
            {
                if (!string.IsNullOrEmpty(criteria.OptionValue))
                {
                    var hasOption = candidate.SelectOptions.Any(o => o.Equals(criteria.OptionValue, StringComparison.OrdinalIgnoreCase));
                    if (hasOption) domainScore += 0.06;
                }

                if (criteria.ExpectedOptions.Count > 0)
                {
                    var matches = criteria.ExpectedOptions.Count(exp => candidate.SelectOptions.Any(o => o.Contains(exp, StringComparison.OrdinalIgnoreCase)));
                    var ratio = (double)matches / criteria.ExpectedOptions.Count;
                    domainScore += ratio * 0.04;
                }
            }
            candidate.ScoreBreakdown["DomainOptions"] = domainScore;

            // 8. Form Context Hints (Weight: up to 0.06)
            double contextHintScore = 0.0;
            if (criteria.ContextHints.Count > 0)
            {
                foreach (var hint in criteria.ContextHints)
                {
                    if (!string.IsNullOrEmpty(candidate.FormId))
                    {
                        if (candidate.FormId.Equals(hint, StringComparison.OrdinalIgnoreCase))
                        {
                            contextHintScore = Math.Max(contextHintScore, 0.06);
                        }
                        else if (candidate.FormId.Contains(hint, StringComparison.OrdinalIgnoreCase))
                        {
                            contextHintScore = Math.Max(contextHintScore, 0.02);
                        }
                    }

                    if (!string.IsNullOrEmpty(candidate.ContainerContextText) &&
                        candidate.ContainerContextText.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    {
                        contextHintScore = Math.Max(contextHintScore, 0.04);
                    }
                }
            }
            candidate.ScoreBreakdown["ContextHints"] = contextHintScore;

            var total = visibilityScore + typeScore + labelScore + optionAndGroupScore + a11yScore + attributeTokenScore + domainScore + contextHintScore;
            candidate.Score = Math.Round(Math.Min(1.0, Math.Max(0.0, total)), 3);
            return candidate.Score;
        }

        /// <summary>
        /// Evaluates a list of candidates, scoring them and selecting the single best candidate.
        /// Throws AmbiguousElementMatchException if two candidates are tied or within ambiguity threshold.
        /// Throws ElementNotFoundException if no candidate achieves the confidence threshold.
        /// </summary>
        public CandidateElement SelectBestCandidate(
            TargetElementCriteria criteria,
            List<CandidateElement> candidates,
            double confidenceThreshold,
            double ambiguityDelta)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new ElementNotFoundException(
                    criteria.SemanticLabel,
                    confidenceThreshold,
                    0.0,
                    "No candidate elements were returned by the DOM locator queries.");
            }

            foreach (var candidate in candidates)
            {
                ScoreCandidate(criteria, candidate);
            }

            var sorted = candidates.OrderByDescending(c => c.Score).ToList();
            var top = sorted[0];

            // Check confidence threshold
            if (top.Score < confidenceThreshold)
            {
                var diagnostics = FormatDiagnostics(criteria, sorted);
                throw new ElementNotFoundException(
                    criteria.SemanticLabel,
                    confidenceThreshold,
                    top.Score,
                    diagnostics);
            }

            // Check ambiguity if 2nd candidate is present and also above confidence threshold
            if (sorted.Count > 1)
            {
                var runnerUp = sorted[1];
                if (runnerUp.Score >= confidenceThreshold)
                {
                    var delta = top.Score - runnerUp.Score;
                    if (delta <= ambiguityDelta)
                    {
                        var report = FormatDiagnostics(criteria, sorted);
                        _logger.LogAmbiguityWarning(
                            criteria.SemanticLabel,
                            top.Score,
                            top.GetIdentifierSummary(),
                            runnerUp.Score,
                            runnerUp.GetIdentifierSummary(),
                            delta);

                        throw new AmbiguousElementMatchException(
                            criteria.SemanticLabel,
                            top.Score,
                            top.GetIdentifierSummary(),
                            runnerUp.Score,
                            runnerUp.GetIdentifierSummary(),
                            delta,
                            report);
                    }
                }
            }

            return top;
        }

        /// <summary>
        /// Text similarity calculator supporting exact match, word boundary matching, token overlap, and abbreviations.
        /// Prevents false substring matches like "male" inside "female".
        /// </summary>
        public static double CalculateTextSimilarity(string candidateText, string targetSemantic, IEnumerable<string>? alternatives = null)
        {
            if (string.IsNullOrWhiteSpace(candidateText) || string.IsNullOrWhiteSpace(targetSemantic))
                return 0.0;

            var cleanCandidate = Normalize(candidateText);
            var cleanTarget = Normalize(targetSemantic);

            // 1. Exact match
            if (cleanCandidate.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            // Check alternatives for exact match
            if (alternatives != null)
            {
                foreach (var alt in alternatives)
                {
                    var cleanAlt = Normalize(alt);
                    if (cleanCandidate.Equals(cleanAlt, StringComparison.OrdinalIgnoreCase))
                        return 0.95;
                }
            }

            // 2. Whole word boundary match (prevents "male" matching inside "female")
            var wordPattern = $@"\b{Regex.Escape(cleanTarget)}\b";
            if (Regex.IsMatch(cleanCandidate, wordPattern, RegexOptions.IgnoreCase))
            {
                return 0.90;
            }

            if (alternatives != null)
            {
                foreach (var alt in alternatives)
                {
                    var cleanAlt = Normalize(alt);
                    var altPattern = $@"\b{Regex.Escape(cleanAlt)}\b";
                    if (Regex.IsMatch(cleanCandidate, altPattern, RegexOptions.IgnoreCase))
                    {
                        return 0.85;
                    }
                }
            }

            // 3. Token-based overlap (Jaccard / Token presence)
            var targetTokens = cleanTarget.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var candidateTokens = cleanCandidate.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (targetTokens.Length > 0 && candidateTokens.Length > 0)
            {
                var matchedTokens = targetTokens.Count(t => candidateTokens.Any(c => c.Equals(t, StringComparison.OrdinalIgnoreCase)));
                if (matchedTokens == targetTokens.Length)
                {
                    return 0.80;
                }
                if (matchedTokens > 0)
                {
                    return 0.35 + (0.35 * matchedTokens / targetTokens.Length);
                }
            }

            // 4. Abbreviation matching (e.g. "fname" for "first name", "lname" for "last name")
            if (IsAbbreviationMatch(cleanCandidate, cleanTarget))
                return 0.80;

            return 0.0;
        }

        private static bool IsAbbreviationMatch(string candidate, string target)
        {
            var targetTokens = target.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (targetTokens.Length >= 2)
            {
                // e.g. "fn" for "first name"
                var acronym = string.Concat(targetTokens.Select(t => t[0])).ToLowerInvariant();
                if (candidate.ToLowerInvariant() == acronym) return true;

                // e.g. "fname" -> first letter of first token + second token ("f" + "name")
                var combined = (targetTokens[0][0] + targetTokens[1]).ToLowerInvariant();
                if (candidate.ToLowerInvariant() == combined) return true;
            }
            return false;
        }

        private static string Normalize(string input)
        {
            return input.Trim().Replace("\r", "").Replace("\n", " ").ToLowerInvariant();
        }

        public static string FormatDiagnostics(TargetElementCriteria criteria, List<CandidateElement> candidates)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Target Semantic: '{criteria.SemanticLabel}' (Expected: {criteria.ExpectedControlType})");
            sb.AppendLine(new string('-', 105));
            sb.AppendLine($"{"#",-3} | {"Tag",-6} | {"Score",-7} | {"Identifiers / Signals",-48} | {"Breakdown"}");
            sb.AppendLine(new string('-', 105));

            int rank = 1;
            foreach (var c in candidates.Take(7))
            {
                var idSummary = c.GetIdentifierSummary();
                if (idSummary.Length > 47) idSummary = idSummary.Substring(0, 44) + "...";

                var breakdownStr = string.Join(", ", c.ScoreBreakdown.Select(kv => $"{kv.Key}:{kv.Value:F2}"));
                sb.AppendLine($"{rank++,-3} | {c.TagName,-6} | {c.Score,6:P1} | {idSummary,-48} | {breakdownStr}");
            }
            sb.AppendLine(new string('-', 105));
            return sb.ToString();
        }
    }
}
