using System;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using ResilientSeleniumAutomation.Core.Configuration;
using ResilientSeleniumAutomation.Core.Logging;

namespace ResilientSeleniumAutomation.Core.Drivers
{
    /// <summary>
    /// Thread-safe factory producing configured Selenium WebDriver instances.
    /// Supports Chrome, Edge, and Firefox with headless flags, optimized timeouts, and CI-friendly settings.
    /// Uses modern Selenium 4 built-in Selenium Manager to automatically resolve drivers.
    /// </summary>
    public static class DriverFactory
    {
        public static IWebDriver CreateDriver(AutomationSettings? settings = null, ITestLogger? logger = null)
        {
            settings ??= ConfigurationManager.Settings;
            logger ??= new ConsoleTestLogger();

            var browserName = settings.Browser.Trim().ToLowerInvariant();
            logger.Info($"Initializing WebDriver for browser '{settings.Browser}' (Headless={settings.Headless})...");

            IWebDriver driver = browserName switch
            {
                "chrome" => CreateChromeDriver(settings),
                "edge" => CreateEdgeDriver(settings),
                "firefox" => CreateFirefoxDriver(settings),
                _ => throw new NotSupportedException($"Browser engine '{settings.Browser}' is not supported. Use Chrome, Edge, or Firefox.")
            };

            // Set timeouts
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            // We intentionally do NOT set an excessive implicit wait, as the ResilientElementFinder
            // performs smart polling and explicit multi-signal evaluation.
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);

            logger.Info($"WebDriver initialized successfully (Session: {((WebDriver)driver).SessionId}).");
            return driver;
        }

        private static IWebDriver CreateChromeDriver(AutomationSettings settings)
        {
            var options = new ChromeOptions();

            if (settings.Headless)
            {
                options.AddArgument("--headless=new");
            }

            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-search-engine-choice-screen");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--remote-allow-origins=*");

            // Page load strategy normal ensures full DOM loading
            options.PageLoadStrategy = PageLoadStrategy.Normal;

            return new ChromeDriver(options);
        }

        private static IWebDriver CreateEdgeDriver(AutomationSettings settings)
        {
            var options = new EdgeOptions();

            if (settings.Headless)
            {
                options.AddArgument("--headless=new");
            }

            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-search-engine-choice-screen");
            options.AddArgument("--remote-allow-origins=*");

            return new EdgeDriver(options);
        }

        private static IWebDriver CreateFirefoxDriver(AutomationSettings settings)
        {
            var options = new FirefoxOptions();

            if (settings.Headless)
            {
                options.AddArgument("-headless");
            }

            options.AddArgument("--width=1920");
            options.AddArgument("--height=1080");

            return new FirefoxDriver(options);
        }
    }
}

