using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using ResilientSeleniumAutomation.Core.Configuration;
using ResilientSeleniumAutomation.Core.Exceptions;
using ResilientSeleniumAutomation.Core.Logging;

namespace ResilientSeleniumAutomation.Core.Resilience
{
    /// <summary>
    /// Resilient Element Identification Engine.
    /// Replaces brittle, single fixed selectors with a multi-signal candidate discovery,
    /// heuristic scoring, bounded retries, and ambiguity-safe selection pipeline.
    /// </summary>
    public class ResilientElementFinder
    {
        private readonly IWebDriver _driver;
        private readonly AutomationSettings _settings;
        private readonly ITestLogger _logger;
        private readonly SignalExtractor _extractor;
        private readonly CandidateScorer _scorer;

        public ResilientElementFinder(
            IWebDriver driver,
            AutomationSettings? settings = null,
            ITestLogger? logger = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _settings = settings ?? ConfigurationManager.Settings;
            _logger = logger ?? new ConsoleTestLogger();
            _extractor = new SignalExtractor(_driver, _logger);
            _scorer = new CandidateScorer(_logger);
        }

        /// <summary>
        /// Locates the single best matching element for the specified semantic criteria.
        /// Handles stale elements, dynamic page rendering, and ambiguities with bounded retries.
        /// </summary>
        public IWebElement FindElement(TargetElementCriteria criteria)
        {
            var attempts = 0;
            var maxAttempts = Math.Max(1, _settings.MaxRetryAttempts);

            while (attempts < maxAttempts)
            {
                attempts++;
                try
                {
                    _logger.Debug($"Resilient find attempt {attempts}/{maxAttempts} for '{criteria.SemanticLabel}' ({criteria.ExpectedControlType})...");

                    var rawCandidates = HarvestRawCandidates(criteria);
                    if (rawCandidates.Count == 0)
                    {
                        if (attempts < maxAttempts)
                        {
                            Thread.Sleep(_settings.PollingIntervalMs);
                            continue;
                        }
                    }

                    var candidateElements = new List<CandidateElement>();
                    foreach (var raw in rawCandidates)
                    {
                        try
                        {
                            var candidate = _extractor.ExtractSignals(raw);
                            candidateElements.Add(candidate);
                        }
                        catch (StaleElementReferenceException)
                        {
                            _logger.Debug("Stale element encountered during signal extraction. Retrying query...");
                            break; // break inner loop to re-harvest
                        }
                    }

                    if (candidateElements.Count == 0 && attempts < maxAttempts)
                    {
                        Thread.Sleep(_settings.PollingIntervalMs);
                        continue;
                    }

                    var best = _scorer.SelectBestCandidate(
                        criteria,
                        candidateElements,
                        _settings.ConfidenceThreshold,
                        _settings.AmbiguityDeltaThreshold);

                    _logger.LogCandidateEvaluation(
                        criteria.SemanticLabel,
                        best.TagName,
                        best.GetIdentifierSummary(),
                        best.Score,
                        accepted: true,
                        rationale: string.Join(", ", best.ScoreBreakdown.Select(kv => $"{kv.Key}:{kv.Value:P0}")));

                    if (_settings.HighlightElementsOnInspection)
                    {
                        HighlightElement(best.Element);
                    }

                    return best.Element;
                }
                catch (AmbiguousElementMatchException)
                {
                    // Ambiguity is a fatal safe-fail: do not retry blindly if markup is genuinely ambiguous
                    throw;
                }
                catch (StaleElementReferenceException ex)
                {
                    _logger.Warn($"Stale element reference on attempt {attempts}/{maxAttempts}: {ex.Message}");
                    if (attempts >= maxAttempts) throw;
                    Thread.Sleep(_settings.PollingIntervalMs * attempts);
                }
                catch (ElementNotFoundException)
                {
                    if (attempts >= maxAttempts) throw;
                    Thread.Sleep(_settings.PollingIntervalMs * attempts);
                }
            }

            throw new ElementNotFoundException(
                criteria.SemanticLabel,
                _settings.ConfidenceThreshold,
                0.0,
                $"Exceeded {maxAttempts} attempts without locating resilient element.");
        }

        /// <summary>
        /// Locates a native &lt;select&gt; dropdown or reports an unsupported custom dropdown accurately.
        /// </summary>
        public DropdownHandler FindDropdown(TargetElementCriteria criteria)
        {
            var element = FindElement(criteria);
            return new DropdownHandler(element, _logger);
        }

        /// <summary>
        /// Types text into a resiliently located text input field.
        /// </summary>
        public void EnterText(TargetElementCriteria criteria, string text, bool clearFirst = true)
        {
            var element = FindElement(criteria);
            ScrollIntoView(element);

            if (clearFirst)
            {
                element.Clear();
            }

            element.SendKeys(text);
            _logger.Info($"Entered value into '{criteria.SemanticLabel}': \"{text}\"");
        }

        /// <summary>
        /// Retrieves current value of a resiliently located input.
        /// </summary>
        public string GetInputValue(TargetElementCriteria criteria)
        {
            var element = FindElement(criteria);
            return element.GetAttribute("value") ?? element.Text ?? string.Empty;
        }

        /// <summary>
        /// Selects a radio button within a logical group.
        /// </summary>
        public void SelectRadioOption(string groupLabel, string optionValue)
        {
            var criteria = TargetElementCriteria.ForRadioOption(groupLabel, optionValue);
            var element = FindElement(criteria);
            ScrollIntoView(element);

            if (!element.Selected)
            {
                try
                {
                    element.Click();
                }
                catch (ElementClickInterceptedException)
                {
                    // Click via JavaScript fallback if obscured by overlay
                    if (_driver is IJavaScriptExecutor js)
                    {
                        js.ExecuteScript("arguments[0].click();", element);
                    }
                    else throw;
                }
            }

            _logger.Info($"Selected radio option '{optionValue}' for group '{groupLabel}'.");
        }

        /// <summary>
        /// Checks if a specific radio option is currently selected.
        /// </summary>
        public bool IsRadioOptionSelected(string groupLabel, string optionValue)
        {
            var criteria = TargetElementCriteria.ForRadioOption(groupLabel, optionValue);
            var element = FindElement(criteria);
            return element.Selected;
        }

        /// <summary>
        /// Broad candidate discovery queries. Does not rely on rigid individual IDs or fixed paths.
        /// </summary>
        private List<IWebElement> HarvestRawCandidates(TargetElementCriteria criteria)
        {
            var results = new List<IWebElement>();

            switch (criteria.ExpectedControlType)
            {
                case ControlType.TextInput:
                    // Broad query for all visible/editable inputs and textareas
                    results.AddRange(_driver.FindElements(By.XPath("//input[not(@type) or @type='text' or @type='email' or @type='search' or @type='tel' or @type='url']")));
                    results.AddRange(_driver.FindElements(By.XPath("//textarea")));
                    break;

                case ControlType.RadioButtonOption:
                case ControlType.RadioButtonGroup:
                    results.AddRange(_driver.FindElements(By.XPath("//input[@type='radio']")));
                    break;

                case ControlType.DropdownSelect:
                    results.AddRange(_driver.FindElements(By.XPath("//select")));
                    break;

                case ControlType.Checkbox:
                    results.AddRange(_driver.FindElements(By.XPath("//input[@type='checkbox']")));
                    break;

                case ControlType.Button:
                    results.AddRange(_driver.FindElements(By.XPath("//button | //input[@type='submit' or @type='button'] | //a[@role='button']")));
                    break;

                default:
                    results.AddRange(_driver.FindElements(By.XPath("//input | //select | //textarea | //button")));
                    break;
            }

            return results;
        }

        private void ScrollIntoView(IWebElement element)
        {
            try
            {
                if (_driver is IJavaScriptExecutor js)
                {
                    js.ExecuteScript("arguments[0].scrollIntoView({behavior: 'instant', block: 'center', inline: 'nearest'});", element);
                }
            }
            catch { }
        }

        private void HighlightElement(IWebElement element)
        {
            try
            {
                if (_driver is IJavaScriptExecutor js)
                {
                    js.ExecuteScript("arguments[0].style.outline = '3px solid #28a745';", element);
                }
            }
            catch { }
        }
    }
}

