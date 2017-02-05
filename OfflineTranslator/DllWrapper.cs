using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AutoTranslate.Core;

namespace OfflineTranslator
{
	internal abstract class DllWrapper : IDisposable
	{
		public const int JapaneseCodepage = 932;
		protected UnmanagedDllLoader Loader = new UnmanagedDllLoader();
		protected string DllPath { get; private set; }
		protected abstract string DllName { get; }
		public string FullDllPath => Path.Combine(DllPath, DllName);

		public virtual string LoadPathFromRegistry()
		{
			return null;
		}

		protected abstract string GetLocalPath();
		protected abstract bool LoadFunctions();

		public bool Init()
		{
			DllPath = LoadPathFromRegistry() ?? GetLocalPath();

			if (!File.Exists(FullDllPath))
			{
				Program.Log($"Tried to load '{FullDllPath}' but it does not exist.");
				return false;
			}

			var succ = Loader.LoadDll(FullDllPath);
			if (!succ)
				return false;
			succ = LoadFunctions();
			return succ && OnInit();
		}

		protected abstract bool OnInit();
		public abstract string Translate(string toTranslate);

		public static IntPtr ConvertStringToNative(string managedString, int codepage)
		{
			var encoding = Encoding.GetEncoding(codepage);
			var buffer = encoding.GetBytes(managedString);
			IntPtr nativeUtf8 = Marshal.AllocHGlobal(buffer.Length + 1);
			Marshal.Copy(buffer, 0, nativeUtf8, buffer.Length);
			Marshal.WriteByte(nativeUtf8, buffer.Length, 0);
			return nativeUtf8;
		}

		public static string ConvertNativeToString(IntPtr nativeUtf8)
		{
			int len = 0;
			while (Marshal.ReadByte(nativeUtf8, len) != 0) ++len;
			byte[] buffer = new byte[len];
			Marshal.Copy(nativeUtf8, buffer, 0, buffer.Length);
			return Encoding.UTF8.GetString(buffer);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				Loader.Dispose();
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}
	}
}
