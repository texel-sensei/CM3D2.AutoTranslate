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
		private static StreamWriter _logFileStream;
		private static string _logfileName = "AutoTranslate.log";
		private static string _logBackupfileName = "AutoTranslate.old.log";

		private static string _dataPath;
		private static string _logFolderName = "Log/";
		private static string LogFolder => Path.Combine(_dataPath, _logFolderName);
		private static string LogFile => Path.Combine(LogFolder, _logfileName);

		public static void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("VerbosityLevel", ref Verbosity);
			section.LoadValue("ColorConsoleOutput", ref ColorOutput);
			section.LoadValue("LogToFile", ref _logToFile);

			try
			{
				if (_logToFile)
				{
					_logFileStream = new StreamWriter(File.Open(LogFile, FileMode.Create, FileAccess.Write));
				}
			}
			catch (Exception e)
			{
				LogError($"Failed to open logfile ${LogFile}", e);
			}
		}

		public static void Init(string path)
		{
			if (!File.Exists(path))
			{
				// assume sybaris
				Log("Data path does not exist, assume sybaris");
				path = Path.Combine(path, @"../../Sybaris/Plugins/UnityInjector/Config/");
			}
			_dataPath = path;
			try
			{
				if (!Directory.Exists(LogFolder))
				{
					Directory.CreateDirectory(LogFolder);
				}
				if (File.Exists(LogFile))
				{
					var bak = Path.Combine(LogFolder, _logBackupfileName);
					if (File.Exists(bak))
					{
						File.Delete(bak);
					}	
					File.Move(LogFile, bak);
					Log($"Created Backup logfile", Level.Debug);
				}
			}
			catch (Exception e)
			{
				_logFileStream = null;
				LogError($"Failed initialization of Logfile {LogFile}", e);
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
			WriteLog($"Log @{Environment.StackTrace}", Level.Error);
		}

		public static void LogError(object msg, Exception e)
		{
			var line = FormatMessage(msg, Level.Error);
			WriteLog(line, Level.Error);
			LogException(e, Level.Error);	
		}

		public static void LogError(Exception e)
		{
			var line = FormatMessage($"Got an {e.GetType().FullName} exception!", Level.Error);
			WriteLog(line, Level.Error);
			LogException(e, Level.Error);
		}

		public static void LogException(Exception e, Level l)
		{
			WriteLog($"Got Exception: {e.GetType().FullName}", l);
			WriteLog(e.Message, l);
			WriteLog($"Log {Environment.StackTrace}", l);
			WriteLog($"\t@{e.TargetSite.Name}", l);
			WriteLog(e.StackTrace, l);
		}

		private static string FormatMessage(object msg, Level level)
		{
			return $"[{CoreUtil.PLUGIN_NAME}] [{CoreUtil.PLUGIN_VERSION}] [{level.ToString()}]: {msg}";
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
				_logFileStream?.Write(msg + Environment.NewLine);
				_logFileStream?.Flush();
			}
			SafeConsole.ForegroundColor = prev;
		}

		private static readonly ConsoleColor[] _colors =
		{
			ConsoleColor.DarkRed,
			ConsoleColor.Gray,
			ConsoleColor.Green,
			ConsoleColor.Yellow,
			ConsoleColor.White,
			ConsoleColor.DarkCyan
		};
	}
}
