using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityInjector.ConsoleUtil;

namespace CM3D2.AutoTranslate.Plugin
{
	public enum Level
	{
		Error = -1,
		General = 0,
		Info,
		Warn,
		Debug,
		Verbose,
	}

	internal static class Logger
	{
		public static int Verbosity = 1;
		public static bool ColorOutput = false;
		private static bool _logToFile = false;
		private static string _logfileName = "AutoTranslate.log";

		private static string _dataPath;
		private static string _logFolderName = "Log/";
		private static string LogFolder => Path.Combine(_dataPath, _logFolderName);
		private static string LogFile => Path.Combine(LogFolder, _logfileName);

		public static void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("VerbosityLevel", ref Verbosity);
			section.LoadValue("ColorConsoleOutput", ref ColorOutput);
			section.LoadValue("LogToFile", ref _logToFile);
		}

		public static void Init(string path)
		{
			_dataPath = path;
			if (!Directory.Exists(LogFolder))
			{
				Directory.CreateDirectory(LogFolder);
			}
		}

		public static void Log(object msg)
		{
			Log(msg, Level.Debug);
		}

		public static void Log(object msg, Level level)
		{
			if ((int)level >= Verbosity)
			{
				return;
			}
			var line = FormatMessage(msg, level);
			WriteLog(line, level);
		}

		public static void LogError(object msg)
		{
			var line = FormatMessage(msg, Level.Error);
			WriteLog(line, Level.Error);
		}

		public static void LogError(Exception e)
		{
			var line = FormatMessage($"Got an {e.GetType().FullName} exception!", Level.Error);
			WriteLog(line, Level.Error);
			WriteLog($"\t@{e.TargetSite.Name}", Level.Error);
			WriteLog(e.StackTrace, Level.Error);
		}

		private static string FormatMessage(object msg, Level level)
		{
			return $"[{CoreUtil.PLUGIN_NAME}] [{level.ToString()}]: {msg}";
		}

		private static void WriteLog(string msg, Level l)
		{
			var prev = SafeConsole.ForegroundColor;
			if (ColorOutput || l == Level.Error)
			{
				SafeConsole.ForegroundColor = _colors[(int) l+1];
			}
			Console.WriteLine(msg);
			if (_logToFile)
			{
				File.AppendAllText(LogFile, msg + Environment.NewLine);
			}
			SafeConsole.ForegroundColor = prev;
		}

		private static readonly ConsoleColor[] _colors =
		{
			ConsoleColor.DarkRed,
			ConsoleColor.White,
			ConsoleColor.Green,
			ConsoleColor.Yellow,
			ConsoleColor.DarkCyan,
			ConsoleColor.Gray
		};
	}
}
