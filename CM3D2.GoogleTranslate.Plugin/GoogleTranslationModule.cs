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
		protected override string Section => "Google";
		private string _targetLanguage = "en";

		public override bool Init()
		{
			// Do Nothing
			return true;
		}

		protected override void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("TargetLanguage", ref _targetLanguage);
		}

		public override IEnumerator Translate(TranslationData data)
		{
			return TranslateGoogle(data.Text, "ja", _targetLanguage, data);
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

			var headers = new Dictionary<string, string> { { "User-Agent", "Mozilla/5.0" }, { "Accept-Charset", "UTF-8" } };
			var www = new WWW(url, null, headers);
			yield return www;


			if (www.error != null)
			{
				CoreUtil.LogError(www.error);
				yield break;
			}
			CoreUtil.Log(www.text, 5);

			html = www.text;

			// First string in json is the translation
			var result = ExtractTranslationFromGoogleString(html);

			result = result.Replace("\\n", "");

			//return WebUtils.DecodeJsString(result);

			// Result is a JavaScript string so we need to deserialize it properly
			//JavaScriptSerializer ser = new JavaScriptSerializer();
			//return ser.Deserialize(result, typeof(string)) as string;
			CoreUtil.Log($"Got Translation from google: {result}", 3);

			translation.Translation = result;
			translation.Success = true;
		}
	}
}
