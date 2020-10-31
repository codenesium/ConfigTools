using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codenesium.ConfigTools
{
    public static class ConfigHelper
    {

        public static string ReadAppSetting(string key)
        {
            if (String.IsNullOrWhiteSpace(key))
            {
                return String.Empty;
            }
            CreateConfigIfMissing();
            var assemblyParent = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string configDirectory = Path.Combine(assemblyParent.FullName, "config");
            string configPath = Path.Combine(configDirectory, "app.config");
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = configPath }, ConfigurationUserLevel.None);
            var appSettingsSection = configuration.GetSection("appSettings") as AppSettingsSection;
            return appSettingsSection.Settings[key]?.Value ?? "";
        }


        public static void WriteAppSetting(string key, string value)
        {
            if (String.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null");
            }


            CreateConfigIfMissing();
            var assemblyParent = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string configDirectory = Path.Combine(assemblyParent.FullName, "config");
            string configPath = Path.Combine(configDirectory, "app.config");
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = configPath }, ConfigurationUserLevel.None);
            var appSettingsSection = configuration.GetSection("appSettings") as AppSettingsSection;

            appSettingsSection.Settings.Remove(key);
            appSettingsSection.Settings.Add(new KeyValueConfigurationElement(key, value));
            configuration.Save(ConfigurationSaveMode.Modified);
        }

        public static string ReadConnectionString(string key)
        {
            if (String.IsNullOrWhiteSpace(key))
            {
                return String.Empty;
            }
            CreateConfigIfMissing();
            var assemblyParent = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string configDirectory = Path.Combine(assemblyParent.FullName, "config");
            string configPath = Path.Combine(configDirectory, "app.config");
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = configPath }, ConfigurationUserLevel.None);
            var connectionStringSection = configuration.GetSection("connectionStrings") as ConnectionStringsSection;
            return connectionStringSection.ConnectionStrings[key]?.ConnectionString ?? "";
        }

        public static void CreateConfigIfMissing()
        {
            var assemblyParent = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string configDirectory = Path.Combine(assemblyParent.FullName, "config");
            string configPath = Path.Combine(configDirectory, "app.config");

            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            if (!File.Exists(configPath))
            {
                var newConfiguration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = configPath }, ConfigurationUserLevel.None);
                newConfiguration.Save();
            }
        }

        public static void WriteConnectionString(string key, string value, string dataProvider)
        {
            CreateConfigIfMissing();
            var assemblyParent = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string configPath = Path.Combine(assemblyParent.FullName, "config", "app.config");
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = configPath }, ConfigurationUserLevel.None);
            var connectionStringSection = configuration.GetSection("connectionStrings") as ConnectionStringsSection;

            if (connectionStringSection.ConnectionStrings[key] == null)
            {
                connectionStringSection.ConnectionStrings.Add(new ConnectionStringSettings(key,value,dataProvider));
            }
            else
            {
                int index = connectionStringSection.ConnectionStrings.IndexOf(connectionStringSection.ConnectionStrings[key]);
                connectionStringSection.ConnectionStrings.RemoveAt(index);
                connectionStringSection.ConnectionStrings.Add(new ConnectionStringSettings(key, value, dataProvider));
            }
            configuration.Save(ConfigurationSaveMode.Modified);
        }

        public static async Task SetConnectionStrings(string filename, Dictionary<string, string> connectionStrings)
        {
            foreach (var key in connectionStrings.Keys)
            {
                await SetConnectionString(filename, key, connectionStrings[key]);
            }
        }

        public static async Task SetAppSettings(string filename, Dictionary<string, string> appSettings)
        {
            foreach (var key in appSettings.Keys)
            {
                await SetAppSetting(filename, key, appSettings[key]);
            }
        }
        public static async Task SetConnectionString(string filename, string key, string value)
        {
            await Task.Run(() =>
            {
                if (!File.Exists(filename))
                {
                    throw new FileNotFoundException($"The configuration file {filename} was not found!");
                }
                ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                configMap.ExeConfigFilename = filename;
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
                var existingExistingConfig = config.ConnectionStrings.ConnectionStrings[key];
                config.ConnectionStrings.ConnectionStrings.Remove(key);

                var connectionStringSetting = new ConnectionStringSettings(key, value);
                connectionStringSetting.ProviderName = existingExistingConfig.ProviderName;
                config.ConnectionStrings.ConnectionStrings.Add(connectionStringSetting);
                config.Save();
            });
        }

        public static async Task SetAppSetting(string filename, string key, string value)
        {
            await Task.Run(() =>
            {
                if (!File.Exists(filename))
                {
                    throw new FileNotFoundException($"The configuration file {filename} was not found!");
                }
                ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                configMap.ExeConfigFilename = filename;
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
                config.AppSettings.Settings.Remove(key);
                config.AppSettings.Settings.Add(key, value);
                config.Save();
            });
        }

        public static async Task SetAppSettingsJSONConnectionString(string filename, string key, string value)
        {
            await Task.Run(() =>
            {
                if (!File.Exists(filename))
                {
                    throw new FileNotFoundException($"The configuration file {filename} was not found!");
                }
                var contents = File.ReadAllText(filename);
                dynamic parsed = (dynamic)JsonConvert.DeserializeObject(contents);
                parsed.ConnectionStrings[key] = value;
                File.WriteAllText(filename, JsonConvert.SerializeObject(parsed, Formatting.Indented));
            });
        }

		/// <summary>
		/// This method will set the value of a property in a json file. It supports nested proeprties
		/// and you can access thoseseparated with a colon like Settings:Child1:Child2 up to 5 in depth.
		/// Since we're using dynamic to make this work I couldn't find a way to not copy and paste this code
		/// over and over.
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static async Task SetAppSettingsJSON(string filename, string key, object value)
		{
			string[] keys = key.Split(':');

			if (!File.Exists(filename))
			{
				throw new FileNotFoundException($"The file {filename} was not found!");
			}

			var contents = File.ReadAllText(filename);
			dynamic parsed = (dynamic)JsonConvert.DeserializeObject(contents);

			var type = value.GetType().DeclaringType;

			if (keys.Length == 1)
			{
				if (value is int)
				{
					parsed[key] = (int)value;
				}
				else if (value is bool)
				{
					parsed[key] = (bool)value;
				}
				else
				{
					parsed[key] = value.ToString();
				}
			}
			else
			{
				if (keys.Length == 2)
				{
					if (value is int)
					{
						parsed[keys[0]][keys[1]] = (int)value;
					}
					else if (value is bool)
					{
						parsed[keys[0]][keys[1]] = (bool)value;
					}
					else
					{
						parsed[keys[0]][keys[1]] = value.ToString();
					}
				}
				else if (keys.Length == 3)
				{
					if (value is int)
					{
						parsed[keys[0]][keys[1]][keys[2]] = (int)value;
					}
					else if (value is bool)
					{
						parsed[keys[0]][keys[1]][keys[2]] = (bool)value;
					}
					else
					{
						parsed[keys[0]][keys[1]][keys[2]] = value.ToString();
					}
				}
				else if (keys.Length == 4)
				{
					if (value is int)
					{
						parsed[keys[0]][keys[1]][keys[2]][keys[3]] = (int)value;
					}
					else if (value is bool)
					{
						parsed[keys[0]][keys[1]][keys[2]][keys[3]] = (bool)value;
					}
					else
					{
						parsed[keys[0]][keys[1]][keys[2]][keys[3]] = value.ToString();
					}
				}
				else if (keys.Length == 5)
				{
					if (value is int)
					{
						parsed[keys[0]][keys[1]][keys[2]][keys[3]][keys[4]] = (int)value;
					}
					else if (value is bool)
					{
						parsed[keys[0]][keys[1]][keys[2]][keys[3]][keys[4]] = (bool)value;
					}
					else
					{
						parsed[keys[0]][keys[1]][keys[2]][keys[3]][keys[4]] = value.ToString();
					}
				}
				else
				{
					throw new Exception($"Key depth of {keys.Length} exceeds the maximum of 5");
				}
			}

			string replacement = JsonConvert.SerializeObject(parsed, Formatting.Indented);

			File.WriteAllText(filename, replacement);
		}
    }
}
