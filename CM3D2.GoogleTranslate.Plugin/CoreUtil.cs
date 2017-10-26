using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityInjector;
using UnityInjector.ConsoleUtil;

namespace CM3D2.AutoTranslate.Plugin
{
	internal static class CoreUtil
	{
		public const string PLUGIN_NAME = "AutoTranslate";
	    public const string PLUGIN_VERSION = "1.3.2";
		private static ExIni.IniFile _preferences;
		private static Dictionary<string,Dictionary<string, string>> _defaultValues = new Dictionary<string, Dictionary<string, string>>();
		private static bool _needSaveConfig = false;

		public static void StartLoadingConfig(ExIni.IniFile pref)
		{
			_preferences = pref;
			LoadConfig();
		}

		private static void LoadConfig()
		{
			var section = LoadSection("Debug");
			Logger.LoadConfig(section);
		}

		public static bool FinishLoadingConfig()
		{
			return _needSaveConfig;
		}

		public static T ChangeType<T>(string obj)
		{
			if (typeof(T).IsEnum)
			{
				return (T)Enum.Parse(typeof(T),obj);
			}
			return (T)Convert.ChangeType(obj, typeof(T));
		}

		public class SectionLoader
		{
			private readonly string _section;
			// ReSharper disable once MemberHidesStaticFromOuterClass
			private readonly ExIni.IniFile _preferences;
			private readonly Dictionary<string, string> _defaults;
			

			public SectionLoader(string section, ExIni.IniFile pref)
			{
				_section = section;
				_preferences = pref;
				if(!_defaultValues.TryGetValue(_section, out _defaults)) { 
					_defaultValues[_section] = _defaults = new Dictionary<string, string>();
				}
			}

			public void LoadValue<T>(string key, ref T val)
			{
				
				if (!_defaults.ContainsKey(key))
				{
					_defaults[key] = val.ToString();
				}

				var entry = _preferences[_section][key];
				Logger.Log($"Loading config value '{_section}'/'{key}' with default '{val}', got: '{entry.Value}'", Level.Debug);
				if (entry.Value == null || entry.Value.Trim() == "")
				{
					entry.Value = val.ToString();
					_needSaveConfig = true;
				}
				try
				{
					val = ChangeType<T>(entry.Value);
				}
				catch (Exception e)
				{
					val = ChangeType<T>(_defaults[key]);
					_needSaveConfig = true;
					Logger.Log(
						$"Invalid value '{val.ToString()}' for Config value '{_section}'/'{key}', resetting to default.",
						Level.Warn
					);
					Logger.LogException(e, Level.Warn);
				}
			}
		}

		public static SectionLoader LoadSection(string section)
		{
			return new SectionLoader(section, _preferences);
		}
	}
}
