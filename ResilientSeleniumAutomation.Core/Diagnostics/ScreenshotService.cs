using System;
using System.IO;
using OpenQA.Selenium;
using ResilientSeleniumAutomation.Core.Configuration;
using ResilientSeleniumAutomation.Core.Logging;

namespace ResilientSeleniumAutomation.Core.Diagnostics
{
    /// <summary>
    /// Service responsible for capturing full viewport screenshots during test failures or diagnostic points.
    /// </summary>
    public static class ScreenshotService
    {
        public static string? CaptureScreenshot(IWebDriver? driver, string testName, AutomationSettings? settings = null, ITestLogger? logger = null)
        {
            if (driver == null)
            {
                return null;
            }

            settings ??= ConfigurationManager.Settings;
            logger ??= new ConsoleTestLogger();

            try
            {
                if (driver is not ITakesScreenshot screenshotDriver)
                {
                    logger.Warn("Driver does not implement ITakesScreenshot.");
                    return null;
                }

                var targetDir = Path.IsPathRooted(settings.ScreenshotsPath)
                    ? settings.ScreenshotsPath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settings.ScreenshotsPath);

                Directory.CreateDirectory(targetDir);

                var sanitizedTestName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{sanitizedTestName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                var fullPath = Path.Combine(targetDir, fileName);

                var screenshot = screenshotDriver.GetScreenshot();
                screenshot.SaveAsFile(fullPath);

                logger.Info($"Screenshot captured successfully: {fullPath}");
                return fullPath;
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to capture screenshot for '{testName}': {ex.Message}");
                return null;
            }
        }
    }
}

