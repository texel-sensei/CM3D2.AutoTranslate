using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CM3D2.Translation;
using CM3D2.Translation.Plugin;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2.AutoTranslate.Plugin
{
	
	[PluginName(PLUGIN_NAME)]
	[PluginVersion("1.0")]
	public class AutoTranslatePlugin : PluginBase
	{
		private const string PLUGIN_NAME = "AutoTranslate";

		private string _translationFile = "google_translated.txt";
		private string _translationFolder = "Translation";
		private string _targetLanguage = "en";
		private int _verbosity = 0;
		private bool _dumpCache = true;

		private readonly Dictionary<string, string> _translationCache = new Dictionary<string, string>();
		private Dictionary<string, string> _alreadyInFile;

		public string DataPathStrings => Path.Combine(this.DataPath, "Strings");
		public string TranslationFilePath => Path.Combine(Path.Combine(DataPathStrings, _translationFolder), _translationFile);

		private void Log(string msg, int level)
		{
			if(level < _verbosity)
			{
				Debug.Log($"{PLUGIN_NAME}: {msg}");
			}
		}

		private void LogError(string msg)
		{
			Debug.LogError($"{PLUGIN_NAME}: {msg}");
		}

		public void Awake()
		{
			DontDestroyOnLoad(this);
			Log("Starting Plugin", 0);
			LoadConfig();
			Log($"Using translation cache file @: {TranslationFilePath}", 1);
			StartCoroutine(HookTranslator());
			LoadCacheFromDisk();
			_alreadyInFile = new Dictionary<string, string>(_translationCache);
		}

		private IEnumerator HookTranslator()
		{
			Log("Waiting for hook...",2);
			yield return new WaitForEndOfFrame();
			Core.TranslateText += TextStreamHandleText;
			Log("Hooked!",2);
			
		}

		private static string ExtractTranslationFromGoogleString(string input)
		{
			var first = input.IndexOf('"')+1;
			var last = first;
			for (; last < input.Length; ++last)
			{
				if (input[last] == '"' && input[last - 1] != '\\')
					break;
			}
			var translation = input.Substring(first, last - first);
			return translation.Replace("\\", "");
		}

		public static T ChangeType<T>(object obj)
		{
			return (T) Convert.ChangeType(obj, typeof(T));
		}

		private bool LoadValue<T>(string key1, string key2, ref T val)
		{
			var entry = Preferences[key1][key2];
			var needSave = false;
			if (string.IsNullOrEmpty(entry.Value))
			{
				entry.Value = val.ToString();
				needSave = true;
			}
			try
			{
				val = ChangeType<T>(entry.Value);
			}
			catch (Exception)
			{
				return true;
			}
			
			return needSave;
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
			var needSave = false;
			needSave |= LoadValue("Cache", "File",  ref _translationFile);
			needSave |= LoadValue("Cache", "Folder", ref _translationFolder);
			needSave |= LoadValue("Cache", "WriteCacheToFile", ref _dumpCache);
			needSave |= LoadValue("Translation", "TargetLanguage", ref _targetLanguage);
			needSave |= LoadValue("Debug", "VerbosityLevel", ref _verbosity);

			if (needSave)
			{
				SaveConfig();
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

		private class TranslationData
		{
			public string Text { get; set; }
			public string Translation { get; set; }
			public bool Success { get; set; }
		}

		/// <summary>
		/// Translates a string into another language using Google's translate API JSON calls.
		/// <seealso>Class TranslationServices</seealso>
		/// </summary>
		/// <param name="Text">Text to translate. Should be a single word or sentence.</param>
		/// <param name="FromCulture">
		/// Two letter culture (en of en-us, fr of fr-ca, de of de-ch)
		/// </param>
		/// <param name="ToCulture">
		/// Two letter culture (as for FromCulture)
		/// </param>
		/// <param name="translation">
		/// The resulting translation or null on error
		/// </param>
		private IEnumerator TranslateGoogle(string text, string fromCulture, string toCulture, TranslationData translation)
		{
			fromCulture = fromCulture.ToLower();
			toCulture = toCulture.ToLower();

			translation.Text = text;
			translation.Success = false;

			// normalize the culture in case something like en-us was passed 
			// retrieve only en since Google doesn't support sub-locales
			string[] tokens = fromCulture.Split('-');
			if (tokens.Length > 1)
				fromCulture = tokens[0];

			// normalize ToCulture
			tokens = toCulture.Split('-');
			if (tokens.Length > 1)
				toCulture = tokens[0];

			//string url =
			//	$@"http://translate.google.com/translate_a/t?client=j&text={WWW.EscapeURL(text)}&hl=en&sl={fromCulture}&tl={toCulture}";

			string url = $@"https://translate.googleapis.com/translate_a/single?client=gtx&sl={fromCulture}&tl={toCulture}&dt=t&q={WWW.EscapeURL(text)}";

			// Retrieve Translation with HTTP GET call
			string html = null;

			var headers = new Dictionary<string, string> {{"User-Agent", "Mozilla/5.0"}, {"Accept-Charset", "UTF-8"} };
			var www = new WWW(url, null, headers);
			yield return www;
			
				
			if (www.error != null)
			{
				LogError(www.error);
				yield break;
			}
			Log(www.text, 5);

			html = www.text;

			// First string in json is the translation
			var result = ExtractTranslationFromGoogleString(html);

			result = result.Replace("\\n", "");

			//return WebUtils.DecodeJsString(result);

			// Result is a JavaScript string so we need to deserialize it properly
			//JavaScriptSerializer ser = new JavaScriptSerializer();
			//return ser.Deserialize(result, typeof(string)) as string;
			Log($"Got Translation from google: {result}", 3);

			translation.Translation = result;
			translation.Success = true;
		}

		private string TextStreamHandleText(object sender, StringTranslationEventArgs e)
		{
			if (e.Handled || e.Text == null || e.Text.Trim().Length == 0)
				return null;
			var translationPluginClass = typeof(TranslationPlugin);

			

			var translationPlugin = (TranslationPlugin) FindObjectOfType(translationPluginClass);
			if (translationPlugin == null)
				LogError("Couldn't find translation plugin");

			var str =
				(string)
				translationPluginClass.GetMethod("OnTranslateString", BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(translationPlugin, new object[2]
					{
						sender,
						e
					});

			Log("Translation Stream: " + str, 4);
			if (str != null)
				return str;

			Log("\tFound no translation for: " + e.Text, 4);

			if (get_ascii_percentage(e.Text) > 0.8)
			{
				Log("\tis ascii, skipping.",4);
				return e.Text;
			}

			var lab = sender as UILabel;
			string translation;
			if (_translationCache.TryGetValue(e.Text, out translation))
			{
				Log("\tgot translation from cache", 4);
				return translation;
			}
			StartCoroutine(DoTranslation(lab, e.Text));
			return e.Text;
		}

		private int _translationId = 0;
		private IEnumerator DoTranslation(UILabel lab, string eText)
		{
			
			var result = new TranslationData();
			var id = _translationId++;
			Log($"Starting translation {id}!",3);
			yield return StartCoroutine(TranslateGoogle(eText, "ja", _targetLanguage, result));
			Log($"Finished Translation {id}!",3);

			CacheTranslation(result);

			if (!lab.isVisible)
			{
				Log($"Label {lab} no longer visible ({id})!",3);
				yield break;
			}		

			if (!result.Success)
			{
				Log($"Failed translation #{id} ({result.Text})!", 2);
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
			if (_dumpCache) { 
				Log("Saving Cache...",1);
				SaveCacheToDisk();
			}
		}
	}
}
