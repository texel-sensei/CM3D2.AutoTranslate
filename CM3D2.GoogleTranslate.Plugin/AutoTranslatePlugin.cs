using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	[PluginVersion("1.0.1")]
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
			DontDestroyOnLoad(this);
			CoreUtil.Log("Starting Plugin", 0);
			LoadConfig();
			if (!_pluginActive)
			{
				CoreUtil.Log("Plugin is disabled.", 0);
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
				CoreUtil.LogError($"Failed to load Translation module '{_activeTranslator}'");
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

			CoreUtil.Log($"Initializing Module {_activeTranslator}", 1);
			success = Translator.Init();
			if (!success)
			{
				CoreUtil.LogError($"Failed to load Translation module {_activeTranslator}");
				Destroy(this);
				return;
			}


			CoreUtil.Log($"Using translation cache file @: {TranslationFilePath}", 1);
			StartCoroutine(HookTranslator());
			LoadCacheFromDisk();
			_alreadyInFile = new Dictionary<string, string>(_translationCache);
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
					CoreUtil.LogError("Translator not implemented!");
					return false;
			}
			return true;
		}

		private IEnumerator HookTranslator()
		{
			CoreUtil.Log("Waiting for hook...",2);
			yield return new WaitForEndOfFrame();
			Core.TranslateText += TextStreamHandleText;
			CoreUtil.Log("Hooked!",2);	
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
				CoreUtil.Log($"Folder {TranslationFolder} does not exist, creating it.", 2);
				Directory.CreateDirectory(TranslationFolder);
			}
			if (!File.Exists(TranslationFilePath))
			{
				CoreUtil.Log($"Cache file {TranslationFilePath} does not exist, creating it.", 2);
				File.Create(TranslationFilePath);
				return;
			}
			foreach (var line in File.ReadAllLines(TranslationFilePath))
			{
				var parts = line.Split('\t');
				_translationCache[parts[0]] = parts[1];
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

			

			var translationPlugin = (TranslationPlugin) FindObjectOfType(translationPluginClass);
			if (translationPlugin == null)
				CoreUtil.LogError("Couldn't find translation plugin");

			var str =
				(string)
				translationPluginClass.GetMethod("OnTranslateString", BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(translationPlugin, new object[2]
					{
						sender,
						e
					});

			CoreUtil.Log("Translation Stream: " + str, 4);
			if (str != null)
				return str;

			CoreUtil.Log("\tFound no translation for: " + e.Text, 4);

			if (get_ascii_percentage(e.Text) > 0.8)
			{
				CoreUtil.Log("\tis ascii, skipping.",4);
				return e.Text;
			}

			var lab = sender as UILabel;
			string translation;
			if (_translationCache.TryGetValue(e.Text, out translation))
			{
				CoreUtil.Log("\tgot translation from cache", 4);
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
			CoreUtil.Log($"Starting translation {id}!",3);
			yield return StartCoroutine(Translator.Translate(result));
			CoreUtil.Log($"Finished Translation {id}!",3);

			CacheTranslation(result);

			if (!lab.isVisible)
			{
				CoreUtil.Log($"Label {lab} no longer visible ({id})!",3);
				yield break;
			}		

			if (!result.Success)
			{
				CoreUtil.Log($"Failed translation #{id} ({result.Text})!", 2);
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
			Translator.DeInit();
			if (_dumpCache) {
				CoreUtil.Log("Saving Cache...",1);
				SaveCacheToDisk();
			}
		}
	}
}
