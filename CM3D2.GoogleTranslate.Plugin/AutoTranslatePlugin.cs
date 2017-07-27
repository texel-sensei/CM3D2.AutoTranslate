using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2.AutoTranslate.Plugin
{

	[PluginName(CoreUtil.PLUGIN_NAME)]
	[PluginVersion(CoreUtil.PLUGIN_VERSION)]
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
		private string _toggleButton = "f10";

		private readonly Dictionary<string, TranslationData> _translationCache = new Dictionary<string, TranslationData>();
		private readonly Dictionary<UILabel, int> _mostRecentTranslations = new Dictionary<UILabel, int>();
		private int _unsavedTranslations = 0;

		private bool _doTranslations = true;

		internal TranslationModule Translator { get; set; }

		private TextPreprocessor _preprocessor = new TextPreprocessor();

		public void Awake()
		{
			Logger.Init(this.DataPath, Preferences);
			try
			{
				DontDestroyOnLoad(this);
				LoadConfig();
				Logger.Log($"Starting {CoreUtil.PLUGIN_NAME} v{CoreUtil.PLUGIN_VERSION}", Level.General);
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
				_preprocessor.LoadConfig();
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
			
				if (_dumpCache && _cacheDumpFrequenzy == CacheDumpFrequenzy.Periodic)
				{
					StartCoroutine(PeriodicDumpCache());
				}

				if (_preprocessor.Init(DataPath))
				{
					Logger.Log("Successfully loaded text preprocessor", Level.Info);
				}
			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
		}

		public void Update()
		{
			if (Input.GetKeyDown(_toggleButton))
			{
				_doTranslations = !_doTranslations;
				Logger.Log("Translations are " + (_doTranslations ? "enabled" : "disabled"), Level.General);
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
				if (_unsavedTranslations == 0)
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
			foreach (var data in GetUnsavedTranslations())
			{
				File.AppendAllText(TranslationFilePath, data.GetCacheLine());
				data.SavedOnDisk = true;
			}
			_unsavedTranslations = 0;
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
				else
				{
				    if (_translationCache.ContainsKey(parts[0]))
				    {
				        Logger.Log($"Line {lineNr} contains a duplicate translation!", Level.Warn);
				    }
				    var orig = parts[0];
				    var transl = parts[1];
					_translationCache[orig] = new TranslationData()
					{
						OriginalText = orig,
						ProcessedText = orig,
						Translation = transl,
						State = TranslationState.Finished,
						SavedOnDisk = true
					};
				}
				lineNr++;
			}
            Logger.Log($"Loaded {_translationCache.Count} translations from cache ({lineNr-1} lines).", Level.Info);
		}

		private IEnumerable<TranslationData> GetUnsavedTranslations()
		{
			foreach (var item in _translationCache)
			{
				var r = item.Value;
				if (r.SavedOnDisk || r.State != TranslationState.Finished) continue;
				yield return item.Value;
			}
		}

		private void LoadConfig()
		{
			CoreUtil.StartLoadingConfig(Preferences);

			var general = CoreUtil.LoadSection("General");
			general.LoadValue("PluginActive", ref _pluginActive);
			general.LoadValue("ToggleTranslationKey", ref _toggleButton);
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
			int check_len = 0;
			foreach (var ch in str)
			{
				if(!char.IsWhiteSpace(ch)) {
					check_len++;
					if (ch >= 0 && ch <= sbyte.MaxValue)
						++num;
				}
			}
			return num / (float) check_len;
		}


		// This function is called via reflection
		// ReSharper disable once UnusedMember.Local
		private string TextStreamHandleText(object sender, object eventArgs)
		{
			var text = HookHelper.GetTextFromEvent(eventArgs);
			if (text == null || text.Trim().Length == 0)
				return null;
			if (get_ascii_percentage(text) > 0.8)
			{
				return text;
			}

            Logger.Log($"Trying {HookHelper.TranslationPlugin}", Level.Verbose);
			var str = HookHelper.CallOriginalTranslator(sender, eventArgs);
			if (str != null)
			{
				Logger.Log($"Got existing Translation from plugin '{str}'", Level.Verbose);
				if (get_ascii_percentage(str) > 0.5)
				{
					return str;
				}
				Logger.Log("Translation from TranslationLoader is not translated!", Level.Warn);
			}
			else
			{
				Logger.Log("\tOriginal Plugin has no translation: " + text, Level.Verbose);
			}

			if (!_doTranslations) return null;

			var lab = sender as UILabel;
			TranslationData translation;

		    var searchtext = text.Replace("\n", "");

			if (_translationCache.TryGetValue(searchtext, out translation))
			{
				Logger.Log("\tFound translation in cache.", Level.Verbose);
				switch (translation.State)
				{
					case TranslationState.Finished:
						return translation.Translation;
					case TranslationState.InProgress:
						Logger.Log("\tTranslation is still in progress.",Level.Debug);
						return null;
					case TranslationState.None:
						Logger.Log("\tTranslation has state none!", Level.Warn);
						return null;
					case TranslationState.Failed:
						Logger.Log("\tTranslation failed before! Retrying...", Level.Warn);
						break;
					default:
						Logger.LogError("Invalid translation state!");
						return null;
				}
			}

			StartCoroutine(DoTranslation(lab, text));
			return text;
		}


		private IEnumerator DoTranslation(UILabel lab, string eText)
		{
			var result = new TranslationData
			{
				Id = TranslationData.AllocateId(),
				ProcessedText = _preprocessor.Preprocess(eText),
				OriginalText =  eText,
				State = TranslationState.InProgress,
				Label = lab
			};

			var id = result.Id;
			Logger.Log($"Starting translation {id}!",Level.Debug);

			_mostRecentTranslations[lab] = id;

			yield return StartCoroutine(Translator.Translate(result));

			Logger.Log($"Finished Translation {id}!",Level.Debug);

			CacheTranslation(result);

			if (result.State != TranslationState.Finished)
			{
				Logger.Log($"Failed translation #{id} ({result.OriginalText})!", Level.Warn);
				yield break;
			}

			if (_mostRecentTranslations[lab] == id)
			{
				lab.text = result.Translation;
				lab.useFloatSpacing = false;
				lab.spacingX = -1;
			}
			else
			{
				Logger.Log("A newer translation request for this label exists.");
			}
		}

		private void CacheTranslation(TranslationData result)
		{
		    if (result.State != TranslationState.Finished)
		    {
                Logger.Log($"Trying to cache unfinised translation #{result.Id}!", Level.Warn);
		        return;
		    }

			_translationCache[result.OriginalText] = result;

			if (_cacheDumpFrequenzy != CacheDumpFrequenzy.Instant || !_dumpCache) return;

			result.SavedOnDisk = true;
			File.AppendAllText(TranslationFilePath,result.GetCacheLine());
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
