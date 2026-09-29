using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Bookstore.Data
{
    /// <summary>
    /// ASP.NET Core replacement for the legacy singleton that read ConfigurationManager.
    /// Register as a singleton in DI and inject where needed.
    /// Settings are read from IConfiguration (appsettings.json / environment / SSM).
    /// Environment-variable overrides are preserved: if an env var with the same key
    /// exists, it wins (matching the legacy behaviour).
    /// </summary>
    public sealed class BookstoreConfiguration
    {
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectionStringOverrides = new Dictionary<string, string>();

        public BookstoreConfiguration(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Programmatically add or override a setting at runtime.
        /// </summary>
        public void AddSetting(string key, string value)
        {
            _overrides[key] = value;
        }

        /// <summary>
        /// Retrieve a setting by key.  Resolution order:
        /// 1. Runtime overrides (AddSetting)
        /// 2. Environment variable with the same name
        /// 3. IConfiguration (appsettings.json, SSM, etc.)
        /// </summary>
        public string GetSetting(string key)
        {
            if (_overrides.TryGetValue(key, out var overrideValue))
            {
                return overrideValue;
            }

            var envValue = Environment.GetEnvironmentVariable(key);
            if (envValue != null)
            {
                return envValue;
            }

            return _configuration[key];
        }

        public T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// Programmatically add or override a connection string at runtime.
        /// </summary>
        public void AddConnectionString(string key, string value)
        {
            _connectionStringOverrides[key] = value;
        }

        public string GetConnectionString(string key)
        {
            if (_connectionStringOverrides.TryGetValue(key, out var overrideValue))
            {
                return overrideValue;
            }

            return _configuration.GetConnectionString(key);
        }
    }
}