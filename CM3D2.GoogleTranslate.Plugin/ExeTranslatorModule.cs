using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	class ExeTranslatorModule : TranslationModule
	{
		protected override string Section => "ExeTranslator";

		private string _exeName = " ";

		protected override void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("Executable", ref _exeName);
		}

		public override bool Init()
		{
			CoreUtil.LogError("Executable loading not yet supported!");
			return false;
			CoreUtil.Log($"Loading '{_exeName}'", 2);
			Process p = new Process()
			{
				StartInfo = new ProcessStartInfo(_exeName)
				{
					UseShellExecute = false,
				//	RedirectStandardInput = true,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
					Arguments = "Some Text",
				}
			};
			p.OutputDataReceived += (sender, args) => CoreUtil.Log($"Got Data: {sender.ToString()}, {args.Data}", 2);
			p.Start();
			CoreUtil.Log(p.ToString(), 2);
			p.BeginOutputReadLine();
			p.WaitForExit();
			return true;
		}

		public override IEnumerator Translate(TranslationData data)
		{
			throw new NotImplementedException();
		}

		public override void DeInit()
		{
			
		}
		
	}
}
