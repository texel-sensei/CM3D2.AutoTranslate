using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{

	internal class TranslationData
	{
		public string Text { get; set; }
		public string Translation { get; set; }
		public bool Success { get; set; }
	}

	internal abstract class TranslationModule
	{
		public abstract bool Init();
		public abstract IEnumerator Translate(TranslationData data);
		public abstract void DeInit();

		protected abstract string Section { get; }
		protected abstract void LoadConfig(CoreUtil.SectionLoader section);

		public void LoadConfig()
		{
			var section = CoreUtil.LoadSection(Section);
			LoadConfig(section);
		}
	}
}
