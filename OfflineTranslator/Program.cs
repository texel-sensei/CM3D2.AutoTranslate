using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OfflineTranslator
{
	internal class Program
	{
		private static void Log(string msg)
		{
			Debug.WriteLine(msg);
			Console.WriteLine(msg);
			File.AppendAllText(@"T:\log.txt", msg + "\n");
		}

		private static void Main(string[] args)
		{
			File.Delete(@"T:\log.txt");
			Log("Hello World!");
			foreach (var arg in args)
			{
				Log(arg);
			}
		}
	}
}