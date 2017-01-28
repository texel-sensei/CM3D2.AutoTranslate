using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	internal class ExeTranslatorModule : TranslationModule
	{
		public override string Section => "ExeTranslator";

		private string _exeName = " ";

		protected override void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("Executable", ref _exeName);
		}

		public override bool Init()
		{
			Logger.LogError("Executable loading not yet supported!");
			return false;
			Logger.Log($"Loading '{_exeName}'", Level.Info);
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
			p.OutputDataReceived += (sender, args) => Logger.Log($"Got Data: {sender.ToString()}, {args.Data}", Level.Verbose);
			p.Start();
			Logger.Log(p.ToString(), Level.Debug);
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
