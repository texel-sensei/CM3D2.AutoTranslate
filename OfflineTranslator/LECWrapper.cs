using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace OfflineTranslator
{
	class LECWrapper : DllWrapper
	{
		protected override string DllName => "EngineDll_je.dll";

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_init(string path);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_init2(string path, int i);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_end();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_translate_multi(int i, IntPtr in_str, int out_size, StringBuilder out_str);

		private eg_end end;
		private eg_translate_multi translate;
		private eg_init init;
		private eg_init2 init2;

		private string PreprocessString(string str)
		{
			var builder = new StringBuilder(str.Length);

			foreach (var c in str)
			{
				char o;

				switch (c)
				{
					case '『':
					case '｢':
					case '「':
						o = '['; break;
					case '』':
					case '｣':
					case '」':
						o = ']'; break;
					case '≪':
					case '（':
						o = '('; break;
					case '≫':
					case '）':
						o = ')'; break;
					case '…':
						o = ' '; break;
					case '：':
						o = '￤'; break;
					case '・':
						o = '.'; break;
					default:
						o = c;
						break;
				}
				builder.Append(c);
			}

			return builder.ToString();
		}

		public override string Translate(string toTranslate)
		{
			toTranslate = PreprocessString(toTranslate);
			var size = toTranslate.Length * 3;
			var builder = new StringBuilder();
			int translatedSize;
			// we can't know the output size, so just try until we have a big enough buffer
			do
			{
				size = size * 2;
				// give up when we reach 10 MB string
				if (size > 10 * 1024 * 1024)
				{
					return null;
				}

				builder.Capacity = size;
				var str = ConvertStringToNative(toTranslate, JapaneseCodepage);
				translatedSize = translate(0, str, size, builder);
				Marshal.FreeHGlobal(str);
			} while (translatedSize > size);
			return builder.ToString();
		}

		public override string LoadPathFromRegistry()
		{
			var path = (string) Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\LogoMedia\LEC Power Translator 15\Configuration",
				"ApplicationPath",null);

			if (path == null)
				return null;

			var up = Directory.GetParent(path.TrimEnd('\\'));
	
			const string subpath = @"Nova\JaEn\";
			var p = Path.Combine(up.FullName, subpath);

			return p;
		}

		protected override string GetLocalPath()
		{
			const string subpath = @"Plugin\Nova\JaEn";

			return Path.Combine(Directory.GetCurrentDirectory(), subpath);
		}

		protected override bool LoadFunctions()
		{
			try
			{
				Loader.LoadFunction("eg_end", out end);
				Loader.LoadFunction("eg_translate_multi", out translate);
			}
			catch (Exception e)
			{
				Program.Log("Failed to load LEC!");
				Program.Log(e);
				return false;
			}

			try
			{
				Loader.LoadFunction("eg_init2", out init2);
			}
			catch
			{
				try
				{
					Loader.LoadFunction("eg_init", out init);
				}
				catch
				{
					return false;
				}
			}

			return true;
		}

		protected override bool OnInit()
		{
			var succ = init2?.Invoke(DllPath, 0);
			var inited = succ ?? init(DllPath);
			return inited == 0;
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing)
				end?.Invoke();
			base.Dispose(disposing);
		}
	}
}
