using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin
{
    [Serializable]
    public class trans
    {
        public string q;
        public string source;
        public string target;
        public string format;
    }

    internal class GoogleTranslationModule : TranslationModule
	{
		public override string Section => "Google";
		private string _targetLanguage = "en";
        private string _apiKey = "";

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
            section.LoadValue("APIKey", ref _apiKey);
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
            var translatedText = data["data"]["translations"][0]["translatedText"];
            return translatedText;
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

            trans tr = new trans();
            tr.q = text;
            tr.source = fromCulture;
            tr.target = toCulture;
            tr.format = "text";

            string transjson = JsonUtility.ToJson(tr);
            var bytes = Encoding.UTF8.GetBytes(transjson);

            string url = "https://translation.googleapis.com/language/translate/v2?key=" + _apiKey;

            var headers = new Dictionary<string, string> { { "User-Agent", "Mozilla/5.0" }, { "Accept-Charset", "UTF-8" } };
			var www = new WWW(url, bytes, headers);
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
