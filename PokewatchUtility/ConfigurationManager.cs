using System;
using System.IO;
using System.Web.Script.Serialization;
using PokewatchUtility.DataTypes;

namespace PokewatchUtility
{
	public class ConfigurationManager
	{
		public static Configuration ReadConfiguration(string signature, string fileName = "Configuration.json")
		{
			PokewatchLogger.Log("[!]Reading configuration from " + fileName + ".", signature);
			Configuration config;
			try
			{
				string json = File.ReadAllText("Configuration.json");
				config = new JavaScriptSerializer().Deserialize<Configuration>(json);
			}
			catch (Exception ex)
			{
				PokewatchLogger.Log("[-]Unable to load config.", signature);
				PokewatchLogger.Log(ex.Message, signature);
				return null;
			}
			return config;
		}
	}
}
