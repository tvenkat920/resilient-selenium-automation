using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ResilientSeleniumAutomation.Core.Exceptions;
using ResilientSeleniumAutomation.Core.Logging;

namespace ResilientSeleniumAutomation.Core.Resilience
{
    /// <summary>
    /// Specialized handler for HTML dropdown interactions.
    /// Supports native HTML &lt;select&gt; elements and accurately detects and reports unsupported custom dropdowns.
    /// </summary>
    public class DropdownHandler
    {
        private readonly IWebElement _element;
        private readonly ITestLogger _logger;

        public DropdownHandler(IWebElement element, ITestLogger? logger = null)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _logger = logger ?? new ConsoleTestLogger();

            ValidateIsNativeSelect();
        }

        private void ValidateIsNativeSelect()
        {
            var tagName = _element.TagName.ToLowerInvariant();
            if (tagName != "select")
            {
                var id = _element.GetAttribute("id") ?? "";
                var className = _element.GetAttribute("class") ?? "";
                var details = $"<{tagName} id='{id}' class='{className}'>";

                throw new UnsupportedDropdownException(
                    tagName,
                    details,
                    "The element is a custom dropdown (e.g., Bootstrap dropdown, div-based or unordered list menu). " +
                    "Standard HTML <select> automation via SelectElement is only supported on native <select> elements. " +
                    "For custom dropdowns, interact with the trigger element and option list items directly.");
            }
        }

        /// <summary>
        /// Selects an option by visible text or value, with intelligent case-insensitive and trimmed fallback.
        /// </summary>
        public void SelectOption(string optionTextOrValue)
        {
            if (string.IsNullOrWhiteSpace(optionTextOrValue))
            {
                throw new ArgumentException("Option text or value must not be empty.", nameof(optionTextOrValue));
            }

            var select = new SelectElement(_element);
            var trimmedTarget = optionTextOrValue.Trim();

            // 1. Try SelectByText exact match
            try
            {
                select.SelectByText(trimmedTarget);
                _logger.Info($"Selected dropdown option by exact text: '{trimmedTarget}'");
                return;
            }
            catch (NoSuchElementException)
            {
                _logger.Debug($"Exact text match '{trimmedTarget}' not found. Attempting case-insensitive and value matching...");
            }

            // 2. Try SelectByValue exact match
            try
            {
                select.SelectByValue(trimmedTarget);
                _logger.Info($"Selected dropdown option by value: '{trimmedTarget}'");
                return;
            }
            catch (NoSuchElementException)
            {
                _logger.Debug($"Exact value match '{trimmedTarget}' not found.");
            }

            // 3. Fallback: Search all options for case-insensitive or trimmed match
            var options = select.Options;
            var matchingOption = options.FirstOrDefault(o =>
                o.Text.Trim().Equals(trimmedTarget, StringComparison.OrdinalIgnoreCase) ||
                (o.GetAttribute("value") ?? "").Trim().Equals(trimmedTarget, StringComparison.OrdinalIgnoreCase));

            if (matchingOption != null)
            {
                matchingOption.Click();
                _logger.Info($"Selected dropdown option via case-insensitive match: '{matchingOption.Text.Trim()}'");
                return;
            }

            // 4. Fallback: Substring match
            var partialMatch = options.FirstOrDefault(o =>
                o.Text.Trim().Contains(trimmedTarget, StringComparison.OrdinalIgnoreCase));

            if (partialMatch != null)
            {
                partialMatch.Click();
                _logger.Info($"Selected dropdown option via partial text match: '{partialMatch.Text.Trim()}'");
                return;
            }

            // If not found, build informative error with available options
            var available = string.Join(", ", options.Select(o => $"'{o.Text.Trim()}'").Take(15));
            throw new NoSuchElementException(
                $"Option '{optionTextOrValue}' was not found in the dropdown. Available options (first 15): [{available}].");
        }

        /// <summary>
        /// Returns the currently selected option's visible text.
        /// </summary>
        public string GetSelectedOptionText()
        {
            var select = new SelectElement(_element);
            return select.SelectedOption.Text.Trim();
        }

        /// <summary>
        /// Returns the currently selected option's value attribute.
        /// </summary>
        public string GetSelectedOptionValue()
        {
            var select = new SelectElement(_element);
            return select.SelectedOption.GetAttribute("value")?.Trim() ?? "";
        }

        /// <summary>
        /// Returns all visible text options available in the dropdown.
        /// </summary>
        public IReadOnlyList<string> GetAllOptions()
        {
            var select = new SelectElement(_element);
            return select.Options.Select(o => o.Text.Trim()).ToList();
        }
    }
}

