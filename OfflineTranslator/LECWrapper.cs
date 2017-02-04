using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

		public override string Translate(string toTranslate)
		{
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

		protected override string GetLocalPath()
		{
			const string path = @"D:\Program Files (x86)\Power Translator 15\Nova\JaEn";
			return path;
		}

		protected override void LoadFunctions()
		{
			Loader.LoadFunction("eg_end", out end);
			Loader.LoadFunction("eg_translate_multi", out translate);
			Loader.LoadFunction("eg_init", out init);
			Loader.LoadFunction("eg_init2", out init2);
		}

		protected override void OnInit()
		{
			init2(DllPath, 0);
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing)
				end();
			base.Dispose(disposing);
		}
	}
}
