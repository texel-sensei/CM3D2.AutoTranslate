﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2.AutoTranslate.Plugin
{

	[PluginName(CoreUtil.PLUGIN_NAME)]
	[PluginVersion("1.1.1")]
	public class AutoTranslatePlugin : PluginBase
	{

		public string DataPathStrings => Path.Combine(this.DataPath, "Strings");
		public string TranslationFolder => Path.Combine(DataPathStrings, _translationFolder);
		public string TranslationFilePath => Path.Combine(TranslationFolder, _translationFile);

		enum CacheDumpFrequenzy
		{
			OnQuit,
			Periodic,
			Instant
		}

		private string _translationFile = "google_translated.txt";
		private string _translationFolder = "Translation";
		private bool _dumpCache = true;
		private bool _pluginActive = true;
		private string _activeTranslator = "Google";
		private CacheDumpFrequenzy _cacheDumpFrequenzy = CacheDumpFrequenzy.OnQuit;
		private int _cacheDumpPeriodicIntervall = 10;

		private readonly Dictionary<string, string> _translationCache = new Dictionary<string, string>();
		private Dictionary<string, string> _alreadyInFile;

		internal TranslationModule Translator { get; set; }

		

		public void Awake()
		{
			Logger.Init(this.DataPath);
			try
			{
				DontDestroyOnLoad(this);
				LoadConfig();
				Logger.Log("Starting Plugin", Level.General);
				if (!_pluginActive)
				{
					Logger.Log("Plugin is disabled.", Level.General);
					if (CoreUtil.FinishLoadingConfig())
					{
						SaveConfig();
					}
					Destroy(this);
					return;
				}

				var success = LoadTranslator();
				if (!success)
				{
					Logger.LogError($"Failed to load Translation module '{_activeTranslator}'");
					if (CoreUtil.FinishLoadingConfig())
					{
						SaveConfig();
					}
					Destroy(this);
					return;
				}

				Translator.LoadConfig();
				if (CoreUtil.FinishLoadingConfig())
				{
					SaveConfig();
				}

				HookHelper.DetectTranslationPlugin();
				if (HookHelper.TranslationPlugin == HookHelper.ParentTranslationPlugin.None)
				{
					Logger.LogError("Found neither Unified Translation Loader nor TranslationPlus! Make sure one of them is installed");
					Destroy(this);
					return;
				}

				Logger.Log($"Initializing Module {_activeTranslator}", Level.Info);
				success = Translator.Init();
				if (!success)
				{
					Logger.LogError($"Failed to load Translation module {_activeTranslator}");
					Destroy(this);
					return;
				}

				StartCoroutine(HookTranslator());

				Logger.Log($"Using translation cache file @: {TranslationFilePath}", Level.Info);
				LoadCacheFromDisk();
				_alreadyInFile = new Dictionary<string, string>(_translationCache);

				if (_dumpCache && _cacheDumpFrequenzy == CacheDumpFrequenzy.Periodic)
				{
					StartCoroutine(PeriodicDumpCache());
				}
			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
		}

		private bool LoadTranslator()
		{
			Translator = TranslationModuleFactory.Create(_activeTranslator);
			if(Translator == null) { 
				Logger.LogError("Translator not implemented!");
				return false;
			}
			Translator._plugin = this;
			return true;
		}

		private IEnumerator PeriodicDumpCache()
		{
			while (true)
			{
				yield return new WaitForSeconds(_cacheDumpPeriodicIntervall);
				if (_alreadyInFile.Count == _translationCache.Count)
				{
					continue;
				}
				Logger.Log("Writing cache to HDD.", Level.Info);
				SaveCacheToDisk();
			}
		}

		private IEnumerator HookTranslator()
		{
			Logger.Log("Waiting for hook...",Level.Info);
			yield return new WaitForEndOfFrame();

			try
			{
				var methodInfo = this.GetType().GetMethod("TextStreamHandleText",
					BindingFlags.NonPublic | BindingFlags.Instance);
				HookHelper.HookTranslationEvent(this, methodInfo);
			}
			catch (Exception e)
			{
				Logger.LogError("Failed hook!",e);
				Destroy(this);
				yield break;
			}
			Logger.Log("Hook successful!",Level.Info);	
		}

		private void SaveCacheToDisk()
		{
			foreach (var line in GetTranslationLines())
			{
				File.AppendAllText(TranslationFilePath, line + Environment.NewLine);	
			}
			_alreadyInFile = new Dictionary<string, string>(_translationCache);
		}

		private void LoadCacheFromDisk()
		{

			if (!Directory.Exists(TranslationFolder))
			{
				Logger.Log($"Folder {TranslationFolder} does not exist, creating it.", Level.Info);
				Directory.CreateDirectory(TranslationFolder);
			}
			if (!File.Exists(TranslationFilePath))
			{
				Logger.Log($"Cache file {TranslationFilePath} does not exist, creating it.", Level.Info);
				File.Create(TranslationFilePath);
				return;
			}
			var lineNr = 1;
			foreach (var line in File.ReadAllLines(TranslationFilePath))
			{
				var parts = line.Split('\t');
				if (parts.Length < 2)
				{
					Logger.Log($"Cache line (Line {lineNr}) is invalid! It contains no tab character!", Level.Warn);
					Logger.Log($"Offending line is \"{line}\"", Level.Warn);
				}
				else if (parts.Length > 2)
				{
					Logger.Log($"Cache line (Line {lineNr}) is invalid! It contains more than one tab character!", Level.Warn);
					Logger.Log($"Offending line is \"{line}\"", Level.Warn);
				}
				else { 
					_translationCache[parts[0]] = parts[1];
				}
				lineNr++;
			}
		}

		private IEnumerable<string> GetTranslationLines()
		{
			foreach (var item in _translationCache)
			{
				if (_alreadyInFile.ContainsKey(item.Key)) continue;
				yield return $"{item.Key}\t{item.Value}".Replace("\n","");
			}
		}

		private void LoadConfig()
		{
			CoreUtil.StartLoadingConfig(Preferences);

			var general = CoreUtil.LoadSection("General");
			general.LoadValue("PluginActive", ref _pluginActive);
			general.LoadValue("TranslationMethod", ref _activeTranslator);

			var cache = CoreUtil.LoadSection("Cache");
			cache.LoadValue("File", ref _translationFile);
			cache.LoadValue("Folder", ref _translationFolder);
			cache.LoadValue("WriteCacheToFile", ref _dumpCache);
			cache.LoadValue("Frequenzy", ref _cacheDumpFrequenzy);
			if(_cacheDumpFrequenzy == CacheDumpFrequenzy.Periodic) { 
				cache.LoadValue("PeriodicIntervall", ref _cacheDumpPeriodicIntervall);
			}
		}

		private static float get_ascii_percentage(string str)
		{
			int num = 0;
			foreach (var ch in str)
			{
				if (ch >= 0 && ch <= sbyte.MaxValue)
					++num;
			}
			return num / (float) str.Length;
		}


		private string TextStreamHandleText(object sender, object eventArgs)
		{
			var text = HookHelper.GetTextFromEvent(eventArgs);
			if (text == null || text.Trim().Length == 0)
				return null;
			
			if (get_ascii_percentage(text) > 0.8)
			{
				Logger.Log($"{text} is ascii, skipping.", Level.Verbose);
				return text;
			}
			var str = HookHelper.CallOriginalTranslator(sender, eventArgs);
			
			Logger.Log("Translation Stream: " + str, Level.Verbose);
			if (str != null)
				return str;

			Logger.Log("\tFound no translation for: " + text, Level.Verbose);

			

			var lab = sender as UILabel;
			string translation;
			if (_translationCache.TryGetValue(text, out translation))
			{
				Logger.Log("\tgot translation from cache", Level.Verbose);
				return translation;
			}
			StartCoroutine(DoTranslation(lab, text));
			return text;
		}

		private int _translationId = 0;

		private IEnumerator DoTranslation(UILabel lab, string eText)
		{
			
			var result = new TranslationData();
			result.Text = eText;
			var id = _translationId++;
			result.Id = id;
			Logger.Log($"Starting translation {id}!",Level.Debug);
			yield return StartCoroutine(Translator.Translate(result));
			Logger.Log($"Finished Translation {id}!",Level.Debug);

			CacheTranslation(result);

			if (!result.Success)
			{
				Logger.Log($"Failed translation #{id} ({result.Text})!", Level.Warn);
				yield break;
			}
			
			lab.text = result.Translation;
			lab.useFloatSpacing = false;
			lab.spacingX = -1;
		}

		private void CacheTranslation(TranslationData result)
		{
			if (!result.Success) return;

			_translationCache[result.Text] = result.Translation;

			if (_cacheDumpFrequenzy == CacheDumpFrequenzy.Instant && _dumpCache)
			{
				_alreadyInFile[result.Text] = result.Translation;
				File.AppendAllText(TranslationFilePath,
					$"{result.Text}\t{result.Translation}".Replace("\n", "") + Environment.NewLine);
			}
		}

		public void OnApplicationQuit()
		{
			try
			{
				Translator.DeInit();
				if (_dumpCache) {
					Logger.Log("Saving Cache...",Level.Info);
					SaveCacheToDisk();
				}
			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
		}
	}
}
