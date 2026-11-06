using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using ResilientSeleniumAutomation.Core.Configuration;
using ResilientSeleniumAutomation.Core.Diagnostics;
using ResilientSeleniumAutomation.Core.Drivers;
using ResilientSeleniumAutomation.Core.Logging;
using ResilientSeleniumAutomation.Core.Pages;

namespace ResilientSeleniumAutomation.Tests
{
    /// <summary>
    /// Base test class managing WebDriver lifecycle, structured logging,
    /// and automatic failure screenshot capture.
    /// </summary>
    [TestFixture]
    public abstract class BaseTest
    {
        protected IWebDriver Driver { get; private set; } = null!;
        protected AutomationSettings Settings { get; private set; } = null!;
        protected ITestLogger Logger { get; private set; } = null!;
        protected FormPage Page { get; private set; } = null!;

        [SetUp]
        public virtual void SetUp()
        {
            Logger = new ConsoleTestLogger();
            Settings = ConfigurationManager.LoadConfiguration();

            var testName = TestContext.CurrentContext.Test.Name;
            Logger.LogSection($"Starting Test: {testName}");

            Driver = DriverFactory.CreateDriver(Settings, Logger);
            Page = new FormPage(Driver, Settings, Logger);
        }

        [TearDown]
        public virtual void TearDown()
        {
            var testName = TestContext.CurrentContext.Test.Name;
            var testStatus = TestContext.CurrentContext.Result.Outcome.Status;

            try
            {
                if (testStatus == TestStatus.Failed && Settings.CaptureScreenshotOnFailure)
                {
                    Logger.Warn($"Test '{testName}' FAILED. Capturing diagnostic screenshot...");
                    var screenshotPath = ScreenshotService.CaptureScreenshot(Driver, testName, Settings, Logger);
                    if (!string.IsNullOrEmpty(screenshotPath))
                    {
                        TestContext.AddTestAttachment(screenshotPath, "Failure Screenshot");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Screenshot capture failed in TearDown: {ex.Message}");
            }
            finally
            {
                if (Driver != null)
                {
                    try
                    {
                        Logger.Info($"Tearing down WebDriver session...");
                        Driver.Quit();
                        Driver.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Error during driver teardown: {ex.Message}");
                    }
                }

                Logger.Info($"Finished Test '{testName}' with status: {testStatus}\n");
            }
        }
    }
}

