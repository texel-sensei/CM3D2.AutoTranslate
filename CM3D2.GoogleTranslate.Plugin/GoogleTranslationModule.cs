using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin
{
	internal class GoogleTranslationModule : TranslationModule
	{
		public override string Section => "Google";
		private string _targetLanguage = "en";

		public override bool Init()
		{
			// Do Nothing
			StartCoroutine(Test());
			return true;
		}

		private IEnumerator Test()
		{
			var dat = new TranslationData()
			{
				Id = 0,
				ProcessedText = "Hallo Welt"
			};
			var cd = CreateCoroutineEx(TranslateGoogle(dat.ProcessedText, "de", "en", dat));
			yield return cd.coroutine;
			try
			{
				cd.Check();
			    if (dat.State == TranslationState.Finished)
			    {
			        Logger.Log("Google seems OK", Level.Debug);
			    }
			    else
			    {
			        Logger.Log("There seems to be a problem with Google!", Level.Warn);
			    }
            }
			catch (Exception e)
			{
			    Logger.Log("There seems to be a problem with Google!", Level.Warn);
                Logger.Log(e);
			}
		}

		protected override void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("TargetLanguage", ref _targetLanguage);
		}

		public override IEnumerator Translate(TranslationData data)
		{
			var cd = CreateCoroutineEx(TranslateGoogle(data.ProcessedText, "ja", _targetLanguage, data));
			yield return cd.coroutine;
			if (cd.GetException() != null)
			{
				Logger.LogException(cd.GetException(), Level.Warn);
				data.State = TranslationState.Failed;
			}
		}

		public override void DeInit()
		{
			// Do Nothing
		}

		private static string ExtractTranslationFromGoogleString(string input)
		{
			var data = JSON.Parse(input);
			var lineBuilder = new StringBuilder(input.Length);
			foreach (JSONNode entry in data.AsArray[0].AsArray)
			{
				var token = entry.AsArray[0].ToString();

				if (lineBuilder.Length != 0) lineBuilder.Append(" ");
				lineBuilder.Append(token.Substring(1, token.Length - 2));
			}

			var text = lineBuilder.ToString();
			var builder = new StringBuilder(text.Length);
			for (var i = 0; i < text.Length; ++i)
			{
				if (text[i] == '\\')
				{
					// skip this and next token
					if (text[i + 1] != '"')
						i++;
					continue;
				}
				if (text[i] == '"' && text[i - 1] != '\\')
					break;
				builder.Append(text[i]);
			}

			return builder.ToString();
		}

		private IEnumerator TranslateGoogle(string text, string fromCulture, string toCulture, TranslationData translation)
		{
			fromCulture = fromCulture.ToLower();
			toCulture = toCulture.ToLower();

			translation.ProcessedText = text;
			translation.State = TranslationState.Failed;

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

			var headers = new Dictionary<string, string> { { "User-Agent", "Mozilla/5.0" }, { "Accept-Charset", "UTF-8" } };
			var www = new WWW(url, null, headers);
			yield return www;


			if (www.error != null)
			{
				Logger.LogError(www.error);
				yield break;
			}

			var result = ExtractTranslationFromGoogleString(www.text);

			result = result.Replace("\\n", "");

			Logger.Log($"Got Translation from google: {result}", Level.Debug);

			translation.Translation = result;
			translation.State = TranslationState.Finished;
		}
	}
}
