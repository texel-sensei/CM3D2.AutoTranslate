using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin
{

	internal enum TranslationState
	{
		None,
		InProgress,
		Finished,
		Failed
	}

	internal class TranslationData
	{
		private static int MaxID = 0;
		public string ProcessedText { get; set; }
		public string OriginalText { get; set; }
		public string Translation { get; set; }
		public int Id { get; set; }
		public UILabel Label { get; set; }
		public bool SavedOnDisk { get; set; }
		public TranslationState State { get; set; }

		public static int AllocateId()
		{
			return ++MaxID;
		}

		public string GetCacheLine()
		{
			if (State != TranslationState.Finished)
			{
				throw new InvalidOperationException($"Tried to get translation cache line of failed translation! (id {Id})");
			}
			return $"{OriginalText}\t{Translation}".Replace("\n", "") + Environment.NewLine;
		}
	}

	internal abstract class TranslationModule
	{
		public abstract string Section { get; }
		public AutoTranslatePlugin _plugin;

		public void LoadConfig()
		{
			var section = CoreUtil.LoadSection(Section);
			LoadConfig(section);
		}
		protected abstract void LoadConfig(CoreUtil.SectionLoader section);

		public abstract bool Init();
		public abstract IEnumerator Translate(TranslationData data);
		public abstract void DeInit();

		public Coroutine StartCoroutine(IEnumerator e)
		{
			return _plugin.StartCoroutine(e);
		}

		public CoroutineEx CreateCoroutineEx(IEnumerator e)
		{
			return new CoroutineEx(_plugin, e);
		}
	}
}
