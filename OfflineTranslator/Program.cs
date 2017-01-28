using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OfflineTranslator
{


	static class NativeMethods
	{
		[DllImport("kernel32.dll")]
		public static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("kernel32.dll")]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);


		[DllImport("kernel32.dll")]
		public static extern bool FreeLibrary(IntPtr hModule);
	}

	internal class Program
	{
		public static void Log(object msg)
		{
			Debug.WriteLine(msg);
			Console.WriteLine(msg);
		}


		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_init(string path);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_init2(string path, int i);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_end();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int eg_translate_multi(int i, IntPtr in_str, int out_size, StringBuilder out_str);

		static eg_end end;
		static eg_translate_multi translate;
		static eg_init init;
		static eg_init2 init2;
		static string path = @"D:\Program Files (x86)\Power Translator 15\Nova\JaEn";
		private static string dllName = @"EngineDll_je.dll";


		static IntPtr test()
		{
			
			IntPtr pDll = NativeMethods.LoadLibrary(Path.Combine(path, dllName));
			//oh dear, error handling here
			if (pDll == IntPtr.Zero)
			{
				Log("pDll == null");
				return IntPtr.Zero;
			}

			var addr0 = NativeMethods.GetProcAddress(pDll, "eg_init");
			var addr1 = NativeMethods.GetProcAddress(pDll, "eg_init2");
			var addr2 = NativeMethods.GetProcAddress(pDll, "eg_end");
			var addr3 = NativeMethods.GetProcAddress(pDll, "eg_translate_multi");

			if (addr0 == IntPtr.Zero) Log("Failed load 0");
			if (addr1 == IntPtr.Zero) Log("Failed load 1");
			if (addr2 == IntPtr.Zero) Log("Failed load 2");
			if (addr3 == IntPtr.Zero) Log("Failed load 3");


			init = (eg_init)Marshal.GetDelegateForFunctionPointer(addr0, typeof(eg_init));
			init2 = (eg_init2)Marshal.GetDelegateForFunctionPointer(addr1,typeof(eg_init2));
			end = (eg_end)Marshal.GetDelegateForFunctionPointer(addr2, typeof(eg_end));
			translate = (eg_translate_multi)Marshal.GetDelegateForFunctionPointer(addr3, typeof(eg_translate_multi));

	
			return pDll;
		}

		public static IntPtr NativeUtf8FromString(string managedString)
		{
			var encoding = Encoding.GetEncoding(932);
			var buffer = encoding.GetBytes(managedString);
			IntPtr nativeUtf8 = Marshal.AllocHGlobal(buffer.Length+1);
			Marshal.Copy(buffer, 0, nativeUtf8, buffer.Length);
			Marshal.WriteByte(nativeUtf8, buffer.Length,0);
			return nativeUtf8;
		}

		public static string StringFromNativeUtf8(IntPtr nativeUtf8)
		{
			int len = 0;
			while (Marshal.ReadByte(nativeUtf8, len) != 0) ++len;
			byte[] buffer = new byte[len];
			Marshal.Copy(nativeUtf8, buffer, 0, buffer.Length);
			return Encoding.UTF8.GetString(buffer);
		}

		private static void Main(string[] args)
		{
			var server = new TcpListener(IPAddress.Any, 9586);
			server.Start();

			Log("starting listening!");
			/*
			while (true)
			{
				try
				{
					var client = server.AcceptTcpClient();
					var stream = client.GetStream();

					Log("Someone connected!");
					while (client.Connected)
					{
						var pack = TranslationProtocoll.ReadPacket(stream);
						Log($"Got packet #{pack.id} and text {pack.text}");
						pack.method = TranslationProtocoll.PacketMethod.translation;
						pack.translation = "Cool Translation: " + pack.text;
						pack.success = true;
						TranslationProtocoll.SendPacket(pack, stream);

					}
				}
				catch(Exception e)
				{
					Log(e.Message);
				}
			}
			*/

			var stream = new FileStream("messages.json", FileMode.Open);
			var pack = TranslationProtocoll.ReadPacket(stream);
			Log(pack.text);

			var dll = test();

			Log($"Init: {init2(path, 0)}");
			int size = pack.text.Length * 5;
			var builder = new StringBuilder(size);
			Log(size);
			var str = NativeUtf8FromString(pack.text);
			var o = translate(0, str, size, builder);
			Marshal.FreeHGlobal(str);
			Log($"Translate output: {o}");
			Log("Translation: " + builder.ToString());
			end();

			NativeMethods.FreeLibrary(dll);

		}
	}
}