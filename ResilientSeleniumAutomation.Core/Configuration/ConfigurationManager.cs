using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace ResilientSeleniumAutomation.Core.Configuration
{
    /// <summary>
    /// Thread-safe singleton configuration manager providing hierarchical settings resolution:
    /// Command-Line Arguments > Environment Variables (RESILIENT_AUTOMATION_*) > appsettings.json > Built-in Defaults.
    /// </summary>
    public static class ConfigurationManager
    {
        private static AutomationSettings? _settings;
        private static readonly object _syncLock = new();

        /// <summary>
        /// Retrieves the active automation settings, initializing if not already loaded.
        /// </summary>
        public static AutomationSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    lock (_syncLock)
                    {
                        _settings ??= LoadConfiguration();
                    }
                }
                return _settings;
            }
        }

        /// <summary>
        /// Explicitly reloads configuration with optional custom command line arguments or base directory.
        /// </summary>
        public static AutomationSettings LoadConfiguration(string[]? args = null, string? basePath = null)
        {
            basePath ??= AppDomain.CurrentDomain.BaseDirectory;

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath);

            // 1. appsettings.json
            var appsettingsPath = Path.Combine(basePath, "appsettings.json");
            if (File.Exists(appsettingsPath))
            {
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            }
            else
            {
                // Check current working directory if different from BaseDirectory
                var cwdAppsettings = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                if (File.Exists(cwdAppsettings))
                {
                    builder.AddJsonFile(cwdAppsettings, optional: true, reloadOnChange: false);
                }
            }

            // 2. Environment Variables prefixed with RESILIENT_AUTOMATION_
            builder.AddEnvironmentVariables(prefix: "RESILIENT_AUTOMATION_");

            // Also check standard BASE_URL environment variable if present
            var directEnvBaseUrl = Environment.GetEnvironmentVariable("BASE_URL");

            // 3. Command Line Arguments if provided
            if (args != null && args.Length > 0)
            {
                builder.AddCommandLine(args);
            }

            var configuration = builder.Build();

            var loadedSettings = new AutomationSettings();
            configuration.GetSection("AutomationSettings").Bind(loadedSettings);

            // Also bind root-level properties if present in config or env
            configuration.Bind(loadedSettings);

            if (!string.IsNullOrWhiteSpace(directEnvBaseUrl))
            {
                loadedSettings.BaseUrl = directEnvBaseUrl;
            }

            lock (_syncLock)
            {
                _settings = loadedSettings;
            }

            return loadedSettings;
        }

        /// <summary>
        /// Resets the loaded settings (primarily used in unit tests to test configuration overrides).
        /// </summary>
        public static void Reset()
        {
            lock (_syncLock)
            {
                _settings = null;
            }
        }
    }
}

