using System;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ResilientSeleniumAutomation.Core.Configuration;
using ResilientSeleniumAutomation.Core.Logging;
using ResilientSeleniumAutomation.Core.Resilience;

namespace ResilientSeleniumAutomation.Core.Pages
{
    /// <summary>
    /// Production Page Object Model for the AutomationPracticeForm.
    /// Encapsulates page interactions using the ResilientElementFinder engine.
    /// Never hardcodes the application URL.
    /// </summary>
    public class FormPage
    {
        private readonly IWebDriver _driver;
        private readonly AutomationSettings _settings;
        private readonly ITestLogger _logger;
        private readonly ResilientElementFinder _finder;

        // Semantic element descriptors
        private readonly TargetElementCriteria _firstNameCriteria =
            TargetElementCriteria.ForTextInput("First Name", "fname", "First", "Given Name");

        private readonly TargetElementCriteria _stateDropdownCriteria =
            TargetElementCriteria.ForDropdown("State", new[] { "Afganistan", "Argentina", "India", "United States", "United Kingdom" }, "state");

        public FormPage(
            IWebDriver driver,
            AutomationSettings? settings = null,
            ITestLogger? logger = null,
            ResilientElementFinder? finder = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _settings = settings ?? ConfigurationManager.Settings;
            _logger = logger ?? new ConsoleTestLogger();
            _finder = finder ?? new ResilientElementFinder(_driver, _settings, _logger);
        }

        /// <summary>
        /// Navigates to the configured BaseUrl or an optional explicit override URL.
        /// Waits until the DOM readyState is complete.
        /// </summary>
        public void Navigate(string? url = null)
        {
            var targetUrl = !string.IsNullOrWhiteSpace(url) ? url : _settings.BaseUrl;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                throw new InvalidOperationException("BaseUrl is not configured in settings, environment variables, or CLI options.");
            }

            _logger.Info($"Navigating to target URL: {targetUrl}");
            _driver.Navigate().GoToUrl(targetUrl);

            WaitForPageLoad();
            _logger.Info($"Page loaded successfully. Title: '{_driver.Title}'");
        }

        /// <summary>
        /// Enters test data into the resiliently identified First Name field.
        /// </summary>
        public void EnterFirstName(string firstName)
        {
            _logger.Info($"Filling First Name field with: '{firstName}'");
            _finder.EnterText(_firstNameCriteria, firstName);
        }

        /// <summary>
        /// Retrieves the current value in the First Name field.
        /// </summary>
        public string GetFirstName()
        {
            var value = _finder.GetInputValue(_firstNameCriteria);
            _logger.Info($"Retrieved First Name value: '{value}'");
            return value;
        }

        /// <summary>
        /// Selects a specified Gender radio option using its label and group context.
        /// Supported standard options on this form: "Male", "Female", "Transgender".
        /// </summary>
        public void SelectGender(string genderOption)
        {
            _logger.Info($"Selecting Gender option: '{genderOption}'");
            _finder.SelectRadioOption("Gender", genderOption);
        }

        /// <summary>
        /// Checks whether a specific gender radio option is selected.
        /// </summary>
        public bool IsGenderSelected(string genderOption)
        {
            return _finder.IsRadioOptionSelected("Gender", genderOption);
        }

        /// <summary>
        /// Determines which gender radio option is currently selected.
        /// </summary>
        public string? GetSelectedGender()
        {
            var options = new[] { "Male", "Female", "Transgender" };
            foreach (var opt in options)
            {
                if (IsGenderSelected(opt))
                {
                    return opt;
                }
            }
            return null;
        }

        /// <summary>
        /// Selects an option from the resiliently identified State dropdown.
        /// </summary>
        public void SelectState(string stateOption)
        {
            _logger.Info($"Selecting State option: '{stateOption}'");
            var dropdown = _finder.FindDropdown(_stateDropdownCriteria);
            dropdown.SelectOption(stateOption);
        }

        /// <summary>
        /// Retrieves the currently selected option text from the State dropdown.
        /// </summary>
        public string GetSelectedState()
        {
            var dropdown = _finder.FindDropdown(_stateDropdownCriteria);
            var selected = dropdown.GetSelectedOptionText();
            _logger.Info($"Current State dropdown selection: '{selected}'");
            return selected;
        }

        /// <summary>
        /// Attempts to treat a custom Bootstrap button dropdown as a native select to demonstrate
        /// accurate detection and handling of UnsupportedDropdownException.
        /// </summary>
        public DropdownHandler InspectCustomDropdown()
        {
            _logger.Info("Inspecting custom dropdown ('Know about us' button group)...");
            var customDropdownCriteria = new TargetElementCriteria
            {
                SemanticLabel = "Know about us",
                ExpectedControlType = ControlType.Any
            };

            // Locate the custom button
            var buttonElement = _driver.FindElements(By.XPath("//button[contains(text(), 'Know about us')]")).FirstOrDefault();
            if (buttonElement == null)
            {
                throw new NoSuchElementException("Custom button dropdown 'Know about us' not found.");
            }

            // DropdownHandler will validate tag name and throw UnsupportedDropdownException
            return new DropdownHandler(buttonElement, _logger);
        }

        /// <summary>
        /// Waits for the browser document.readyState to equal 'complete'.
        /// </summary>
        public void WaitForPageLoad()
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");
        }
    }
}
