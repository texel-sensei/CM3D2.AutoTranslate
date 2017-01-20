using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityInjector.ConsoleUtil;

namespace CM3D2.AutoTranslate.Plugin
{
	internal static class CoreUtil
	{
		public const string PLUGIN_NAME = "AutoTranslate";
		private static ExIni.IniFile _preferences;
		private static Dictionary<string,Dictionary<string, string>> _defaultValues = new Dictionary<string, Dictionary<string, string>>();
		private static bool _needSaveConfig = false;

		private static int _verbosity = 0;
		private static bool _colorDebugOutput = false;

		public static void Log(string msg, int level)
		{
			if (level < _verbosity)
			{
				var prev = SafeConsole.ForegroundColor;
				if (_colorDebugOutput)
				{
					SafeConsole.ForegroundColor = ConsoleColor.Green;
				}
				var line = $"{PLUGIN_NAME}: {msg}";
				Console.WriteLine(line);
				if (_colorDebugOutput)
				{
					SafeConsole.ForegroundColor = prev;
				}
			}
		}

		public static void StartLoadingConfig(ExIni.IniFile pref)
		{
			_preferences = pref;
			LoadConfig();
		}

		private static void LoadConfig()
		{
			var section = LoadSection("Debug");
			section.LoadValue("VerbosityLevel", ref _verbosity);
			section.LoadValue("ColorConsoleOutput", ref _colorDebugOutput);
		}

		public static bool FinishLoadingConfig()
		{
			return _needSaveConfig;
		}

		public static void LogError(string msg)
		{
			Debug.LogError($"{PLUGIN_NAME}: {msg}");
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
				_defaultValues[_section] = _defaults = new Dictionary<string, string>();
			}

			public void LoadValue<T>(string key, ref T val)
			{
				
				if (!_defaults.ContainsKey(key))
				{
					_defaults[key] = val.ToString();
				}

				var entry = _preferences[_section][key];
				Log($"Loading config value '{_section}'/'{key}' with default '{val}', got: '{entry.Value}'", 7);
				if (string.IsNullOrEmpty(entry.Value))
				{
					entry.Value = val.ToString();
					_needSaveConfig = true;
				}
				try
				{
					val = ChangeType<T>(entry.Value);
				}
				catch (Exception)
				{
					val = ChangeType<T>(_defaults[key]);
					_needSaveConfig = true;
					LogError($"Invalid value '{val.ToString()}' for Config value '{_section}'/'{key}', resetting to default.");
				}
			}
		}

		public static SectionLoader LoadSection(string section)
		{
			return new SectionLoader(section, _preferences);
		}
	}
}
