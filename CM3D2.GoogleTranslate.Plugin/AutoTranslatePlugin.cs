using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
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

		private bool _pluginActive = true;
		private bool _doTranslations = true;
		private bool _dumpCache = true;
		private string _toggleButton = "f10";

		private string _translationFile = "google_translated.txt";
		private string _translationFolder = "Translation";
		private string _activeTranslator = "Google";

	    private const string COMMENT_LINE_START = ";";
	    private const string LEVEL_PREFIX = "$LEVEL";
	    private string _IgnoreFileName = "AutoTranslateIgnore.txt";

        private CacheDumpFrequenzy _cacheDumpFrequenzy = CacheDumpFrequenzy.OnQuit;
		private int _cacheDumpPeriodicIntervall = 10;
		private readonly Dictionary<string, TranslationData> _translationCache = new Dictionary<string, TranslationData>();
		private int _unsavedTranslations = 0;

		private readonly Dictionary<MonoBehaviour, int> _mostRecentTranslations = new Dictionary<MonoBehaviour, int>();

        private readonly HashSet<int> _ignoredLevels = new HashSet<int>();
        private readonly HashSet<string> _ignoredInputs = new HashSet<string>();
	    private bool _translationDisabledForLevel = false;

		internal TranslationModule Translator { get; set; }

		private readonly TextPreprocessor _preprocessor = new TextPreprocessor();

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

                LoadIgnores();

				var translatorPlugin = HookHelper.DetectTranslationPlugin();
				if (translatorPlugin == HookHelper.ParentTranslationPlugin.None)
				{
				    var bldr = new StringBuilder("Found none of the supported translation plugins!\n");
				    bldr.AppendLine("Make sure, that one of the following is installed:");
				    bldr.AppendLine(" - Yet Another Translator (recommended)");
                    bldr.AppendLine(" - Unified Translation loader (only for ReiPatcher)");
				    bldr.AppendLine(" - Translation Plus (only for Sybaris)");
                    Logger.LogError(bldr.ToString());
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
                Destroy(this);
			}
		}

		public void Update()
		{
			if (Input.GetKeyDown(_toggleButton))
			{
			    if (_translationDisabledForLevel)
			    {
			        Logger.Log("Overriding translation ignore for current level!", Level.Warn);
			        _translationDisabledForLevel = false;
			    }
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

	    public void OnLevelWasLoaded(int level)
	    {
            Logger.Log($"Entering level {level}", Level.Debug);
	        if (_ignoredLevels.Contains(level))
	        {
	            _translationDisabledForLevel = true;
	            Logger.Log($"Disabling AutoTranslate for level {level}", Level.General);
	        }
	        else
	        {
	            _translationDisabledForLevel = false;
            }
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

		    var success = true;

			try
			{
				success = HookHelper.HookTranslationEvent(this);
			}
			catch (Exception e)
			{
				Logger.LogError("Exception while hooking:",e);
			    success = false;
			}
		    if (!success)
		    {
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
            general.LoadValue("IgnoreFileName", ref _IgnoreFileName);

			var cache = CoreUtil.LoadSection("Cache");
			cache.LoadValue("File", ref _translationFile);
			cache.LoadValue("Folder", ref _translationFolder);
			cache.LoadValue("WriteCacheToFile", ref _dumpCache);
			cache.LoadValue("Frequenzy", ref _cacheDumpFrequenzy);
			if(_cacheDumpFrequenzy == CacheDumpFrequenzy.Periodic) { 
				cache.LoadValue("PeriodicIntervall", ref _cacheDumpPeriodicIntervall);
			}
		}

	    private void LoadIgnores()
	    {
	        string filePath = Path.Combine(DataPath, _IgnoreFileName);

	        Logger.Log("Reading ignore file", Level.Info);

	        if (!File.Exists(filePath))
	        {
	            using (var sw = File.CreateText(filePath))
	                sw.WriteLine();
	            return;
	        }

	        foreach (var line in File.ReadAllLines(filePath))
	        {
	            var l = line.Trim();
	            if (l.StartsWith(COMMENT_LINE_START))
	                continue;
	            if (l.Length == 0)
	                continue;
	            if (l.StartsWith(LEVEL_PREFIX))
	            {
	                if (int.TryParse(l.Substring(LEVEL_PREFIX.Length), out int level)) { 
                        Logger.Log($"Ignoring text in level {level}", Level.Info);
	                    _ignoredLevels.Add(level);
	                }
                    continue;
	            }
	            Logger.Log($"Ignoring input text '{line}'", Level.Info);
                _ignoredInputs.Add(line);
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

	    public static bool IsAsciiText(string txt)
	    {
	        return get_ascii_percentage(txt) > 0.8;
	    }

	    public bool ShouldTranslateText(string text)
	    {
	        if (_translationDisabledForLevel) return false;
	        if (!_doTranslations) return false;

	        if (_ignoredInputs.Contains(text))
	            return false;

	        if (text == null || text.Trim().Length == 0)
	            return false;
	        return !IsAsciiText(text);
	    }

	    internal TranslationData BuildTranslationData(string text, MonoBehaviour display)
	    {
	        var searchtext = text.Replace("\n", "");

	        if (_translationCache.TryGetValue(searchtext, out var translation))
	        {
	            Logger.Log("\tFound translation in cache.", Level.Verbose);
	            switch (translation.State)
	            {
	                case TranslationState.Finished:
	                    return translation;
	                case TranslationState.InProgress:
	                    Logger.Log("\tTranslation is still in progress.", Level.Debug);
	                    return translation;
                    case TranslationState.None:
	                    Logger.Log("\tTranslation has state none!", Level.Warn);
                        return translation;
                    case TranslationState.Failed:
	                    Logger.Log("\tTranslation failed before! Retrying...", Level.Warn);
	                    break;
	                default:
	                    Logger.LogError("Invalid translation state!");
	                    break;
	            }
	        }
  
	        translation = new TranslationData
	        {
	            Id = TranslationData.AllocateId(),
	            ProcessedText = _preprocessor.Preprocess(text),
	            OriginalText = text,
	            State = TranslationState.InProgress,
	            Display = display
	        };

            return translation;
	    }

	    internal void StartTranslation(TranslationData translation)
	    {
	        StartCoroutine(DoTranslation(translation));
	    }

        private IEnumerator DoTranslation(TranslationData result)
		{
			var id = result.Id;
			Logger.Log($"Starting translation {id}!",Level.Debug);

		    var lab = result.Display;

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
                UpdateDisplay(result, lab);
            }
            else
			{
				Logger.Log("A newer translation request for this label exists.");
			}
		}

        private static void UpdateDisplay(TranslationData result, MonoBehaviour display)
        {
            switch (display)
            {
                case UILabel lab:
                    lab.text = result.Translation;
                    lab.useFloatSpacing = false;
                    lab.spacingX = -1;
                    break;
                case Text txt:
                    txt.text = result.Translation;
                    break;
                default:
                    Logger.Log(
                        $"Translation for unsupported object {display.GetType().Name}"
                        , Level.Warn
                    );
                    break;
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
