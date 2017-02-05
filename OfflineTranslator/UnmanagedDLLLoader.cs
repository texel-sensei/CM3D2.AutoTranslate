using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AutoTranslate.Core
{
	internal static class NativeMethods
	{
		[DllImport("kernel32.dll")]
		public static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("kernel32.dll")]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);


		[DllImport("kernel32.dll")]
		public static extern bool FreeLibrary(IntPtr hModule);
	}

	public class UnmanagedDllLoader : IDisposable
	{
		private IntPtr _dll;

		public bool LoadDll(string path)
		{
			_dll = NativeMethods.LoadLibrary(path);
			return _dll != IntPtr.Zero;
		}

		public void LoadFunction<T>(string name, out T f)
		{
			f = default(T);
			if (_dll == IntPtr.Zero)
			{
				throw new InvalidOperationException($"Tried to load function {name} with no dll!");
			}
			var addr = NativeMethods.GetProcAddress(_dll, name);
			if (addr == IntPtr.Zero)
			{
				throw new InvalidOperationException($"Failed to load function {name}");
			}
			f = (T)(object)Marshal.GetDelegateForFunctionPointer(addr, typeof(T));
		}

		public void Dispose()
		{
			NativeMethods.FreeLibrary(_dll);
		}
	}
}
