using System;

namespace ResilientSeleniumAutomation.Core.Configuration
{
    /// <summary>
    /// Strongly-typed automation configuration settings.
    /// Loaded hierarchically from appsettings.json, environment variables, and CLI parameters.
    /// </summary>
    public class AutomationSettings
    {
        /// <summary>
        /// Base URL for the target web application.
        /// Must be configurable and never hardcoded in page objects.
        /// </summary>
        public string BaseUrl { get; set; } = "https://app.cloudqa.io/home/AutomationPracticeForm";

        /// <summary>
        /// Browser engine: Chrome, Edge, Firefox. Defaults to Chrome.
        /// </summary>
        public string Browser { get; set; } = "Chrome";

        /// <summary>
        /// Whether browser should run in headless mode. Useful for CI/CD environments.
        /// </summary>
        public bool Headless { get; set; } = true;

        /// <summary>
        /// Global explicit wait timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Polling interval in milliseconds for wait conditions and bounded retries.
        /// </summary>
        public int PollingIntervalMs { get; set; } = 250;

        /// <summary>
        /// Maximum number of retry attempts for stale elements or dynamic DOM reloads.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Minimum composite score (0.0 to 1.0) a candidate element must achieve to be considered valid.
        /// Default is 0.65 (65% confidence).
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.65;

        /// <summary>
        /// Ambiguity threshold delta (0.0 to 1.0).
        /// If the difference between the 1st and 2nd best candidate scores is less than this delta,
        /// the engine fails safely and reports ambiguity instead of guessing.
        /// Default is 0.08 (8% score difference).
        /// </summary>
        public double AmbiguityDeltaThreshold { get; set; } = 0.08;

        /// <summary>
        /// Whether to automatically take screenshots on test failure.
        /// </summary>
        public bool CaptureScreenshotOnFailure { get; set; } = true;

        /// <summary>
        /// Target directory path where failure screenshots will be saved.
        /// </summary>
        public string ScreenshotsPath { get; set; } = "TestResults/Screenshots";

        /// <summary>
        /// Target directory path where locator diagnostic reports will be saved.
        /// </summary>
        public string DiagnosticsPath { get; set; } = "TestResults/Diagnostics";

        /// <summary>
        /// Highlight candidate elements with colored border during interactive debugging if true.
        /// </summary>
        public bool HighlightElementsOnInspection { get; set; } = false;
    }
}

