using System;
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

		private int _maxConcurrentTranslationRequests = 5;
		private int _runningTranslationRequests = 0;
		private TranslationData[] newRequests; // all requests that need to be started this frame
		private TranslationQueue _queue = new TranslationQueue();

		// Caching
		private readonly Dictionary<string, TranslationData> _translationCache = new Dictionary<string, TranslationData>();
		private readonly Dictionary<UILabel, int> _mostRecentTranslations = new Dictionary<UILabel, int>();
		private int _unsavedTranslations = 0;

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

				newRequests = new TranslationData[_maxConcurrentTranslationRequests];

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
					_translationCache[parts[0]] = new TranslationData()
					{
						Text = parts[1],
						Translation = parts[0],
						State = TranslationState.Finished,
						SavedOnDisk = true
					};
				}
				lineNr++;
			}
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
			general.LoadValue("TranslationMethod", ref _activeTranslator);
			general.LoadValue("MaxConcurrentTranslations", ref _maxConcurrentTranslationRequests);

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


		// This function is called via reflection
		// ReSharper disable once UnusedMember.Local
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
			TranslationData translation;
			if (_translationCache.TryGetValue(text, out translation))
			{
				Logger.Log("\tFound translation in cache.", Level.Verbose);
				switch (translation.State)
				{
					case TranslationState.Finished:
						Logger.Log("\tIs finished translation.", Level.Verbose);
						return translation.Translation;
					case TranslationState.InProgress:
						Logger.Log("\tTranslation is still in progress.",Level.Verbose);
						return null;
					case TranslationState.None:
						Logger.Log("\tTranslation has state none!", Level.Warn);
						return null;
					default:
						Logger.LogError("Invalid translation state!");
						return null;
				}
			}

			var request = new TranslationData
			{
				Id = TranslationData.AllocateId(),
				Text = text,
				State = TranslationState.InProgress,
				Label = lab
			};
			Logger.Log($"Queueing translation #{request.Id}");
			_queue.AddRequest(request);

			return null;
		}

		public void Update()
		{
			if (_runningTranslationRequests >= _maxConcurrentTranslationRequests)
				return;
			if (_queue.Count() == 0)
				return;

			var n = _maxConcurrentTranslationRequests - _runningTranslationRequests;
			if (n <= 0)
			{
				Logger.LogError($"Somhow got less than 0 requests! ({n})");
				return;
			}
			var p = 0;
			foreach (var request in _queue.GetRequests(n))
			{
				newRequests[p] = request;
				p++;
			}
			Logger.Log($"Starting {p} new requests");
			for (var i = 0; i < p; i++)
			{
				var request = newRequests[i];

				_queue.RemoveRequest(request);
				_runningTranslationRequests++;
				newRequests[i] = null;

				StartCoroutine(DoTranslation(request));
			}
			Logger.Log("Finished Update.");
		}

		private IEnumerator DoTranslation(TranslationData request)
		{
			var lab = request.Label;
			var id = request.Id;
			Logger.Log($"Starting translation {id}!",Level.Debug);

			_mostRecentTranslations[lab] = id;

			yield return Translator.Translate(request);

			Logger.Log($"Finished Translation {id}!",Level.Debug);

			CacheTranslation(request);

			
			_runningTranslationRequests--;

			if (request.State != TranslationState.Finished)
			{
				Logger.Log($"Failed translation #{id} ({request.Text})!", Level.Warn);
				yield break;
			}

			if (_mostRecentTranslations[lab] == id)
			{
				lab.text = request.Translation;
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
			if (result.State != TranslationState.Finished) return;

			_translationCache[result.Text] = result;

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
