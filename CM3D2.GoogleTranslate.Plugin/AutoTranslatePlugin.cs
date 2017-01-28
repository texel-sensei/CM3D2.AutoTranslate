using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CM3D2.Translation;
using CM3D2.Translation.Plugin;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;
using SimpleJSON;

namespace CM3D2.AutoTranslate.Plugin
{
	
	[PluginName(CoreUtil.PLUGIN_NAME)]
	[PluginVersion("1.0.2")]
	public class AutoTranslatePlugin : PluginBase
	{
		enum TranslatorID
		{
			Google,
			Executable,
			GenericServer
		};
		
		public string DataPathStrings => Path.Combine(this.DataPath, "Strings");
		public string TranslationFolder => Path.Combine(DataPathStrings, _translationFolder);
		public string TranslationFilePath => Path.Combine(TranslationFolder, _translationFile);


		private string _translationFile = "google_translated.txt";
		private string _translationFolder = "Translation";
		private bool _dumpCache = true;
		private bool _pluginActive = true;
		private TranslatorID _activeTranslator;

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

				Logger.Log($"Initializing Module {_activeTranslator}", Level.Info);
				success = Translator.Init();
				if (!success)
				{
					Logger.LogError($"Failed to load Translation module {_activeTranslator}");
					Destroy(this);
					return;
				}


				Logger.Log($"Using translation cache file @: {TranslationFilePath}", Level.Info);
				StartCoroutine(HookTranslator());
				LoadCacheFromDisk();
				_alreadyInFile = new Dictionary<string, string>(_translationCache);
			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
		}

		private bool LoadTranslator()
		{
			switch (_activeTranslator)
			{
				case TranslatorID.Google:
					Translator = new GoogleTranslationModule();
					break;
				case TranslatorID.Executable:
					Translator = new ExeTranslatorModule();
					break;
				case TranslatorID.GenericServer:
					Translator = new GenericServerTranslationModule();
					break;
				default:
					Logger.LogError("Translator not implemented!");
					return false;
			}
			Translator._plugin = this;
			return true;
		}

		private IEnumerator HookTranslator()
		{
			Logger.Log("Waiting for hook...",Level.Info);
			yield return new WaitForEndOfFrame();
			Core.TranslateText += TextStreamHandleText;
			Logger.Log("Hooked!",Level.Info);	
		}

		private void SaveCacheToDisk()
		{
			foreach (var line in GetTranslationLines())
			{
				File.AppendAllText(TranslationFilePath, line + Environment.NewLine);	
			}
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


		private string TextStreamHandleText(object sender, StringTranslationEventArgs e)
		{
			if (e.Handled || e.Text == null || e.Text.Trim().Length == 0)
				return null;
			var translationPluginClass = typeof(TranslationPlugin);

			if (get_ascii_percentage(e.Text) > 0.8)
			{
				Logger.Log($"{e.Text} is ascii, skipping.", Level.Verbose);
				return e.Text;
			}

			var translationPlugin = (TranslationPlugin) FindObjectOfType(translationPluginClass);
			if (translationPlugin == null)
				Logger.LogError("Couldn't find translation plugin");

			var str =
				(string)
				translationPluginClass.GetMethod("OnTranslateString", BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(translationPlugin, new object[2]
					{
						sender,
						e
					});

			Logger.Log("Translation Stream: " + str, Level.Verbose);
			if (str != null)
				return str;

			Logger.Log("\tFound no translation for: " + e.Text, Level.Verbose);

			

			var lab = sender as UILabel;
			string translation;
			if (_translationCache.TryGetValue(e.Text, out translation))
			{
				Logger.Log("\tgot translation from cache", Level.Verbose);
				return translation;
			}
			StartCoroutine(DoTranslation(lab, e.Text));
			return e.Text;
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

			if (!lab.isVisible)
			{
				Logger.Log($"Label {lab} no longer visible ({id})!",Level.Debug);
				yield break;
			}		

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
