using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	class TextPreprocessor
	{
		private readonly Dictionary<char, string> _textReplacements = new Dictionary<char, string>();

		private string _replacementsFile = "AutoTranslateTextSubstitutions.txt";
		private bool _valid = false;

		public bool Init(string datapath)
		{
			_valid = LoadReplacementFile(Path.Combine(datapath, _replacementsFile));
			return _valid;
		}

		public void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("SubstitutionsFile", ref _replacementsFile);
		}

		private bool LoadReplacementFile(string full_path)
		{
			Logger.Log($"Using substitutins file {full_path}", Level.Info);
			if (!File.Exists(full_path))
			{
				Logger.Log($"Couldn't find substitutions file '{full_path}'!", Level.Warn);
				return false;
			}
			var lineNr = 0;
			foreach (var line in File.ReadAllLines(full_path))
			{
				lineNr++;
				var parts = line.Split('\t');
				if (parts.Length != 2)
				{
					Logger.Log($"Substitution line (Line {lineNr}) is invalid! It should contain exactly 1 tab character!", Level.Warn);
					Logger.Log($"Offending line is \"{line}\"", Level.Warn);
					continue;
				}
				if (parts[0].Length != 1)
				{
					Logger.Log("Substitutions can only consist of a single character!");
					Logger.Log($"{parts[0]} (line {lineNr}) is invalid!");
					continue;
				}
				_textReplacements[parts[0][0]] = parts[1];
			}
			return _textReplacements.Count > 0;
		}

		public string Preprocess(string input)
		{
			if (!_valid) return input;

			var sb = new StringBuilder(input.Length * 2);

			int last_index = 0;
			for (var i = 0; i < input.Length; i++)
			{
				var c = input[i];
				string repl;

				if (!_textReplacements.TryGetValue(c, out repl)) continue;

				sb.Append(input, last_index, i - last_index);
				sb.Append(repl);
				last_index = i + 1;
			}
			sb.Append(input, last_index, input.Length - last_index);

			return sb.ToString();
		}
	}
}
