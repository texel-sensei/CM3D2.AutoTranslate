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

			var loader = new AutoTranslate.Core.UnmanagedDllLoader();
			var succ = loader.LoadDll(Path.Combine(path, dllName));

			if (!succ)
			{
				Log("Failed loading Dll");
				loader.Dispose();
				return;
			}


			try
			{
				loader.LoadFunction("eg_end", out end);
				loader.LoadFunction("eg_translate_multi", out translate);
				loader.LoadFunction("eg_init", out init);
				loader.LoadFunction("eg_init2", out init2);
			}
			catch (Exception e)
			{
				Log(e);
				return;
			}



			Log($"Init: {init2(path, 0)}");

			while (true)
			{
				try
				{
					var client = server.AcceptTcpClient();
					var stream = client.GetStream();

					Log("Someone connected!");
					while (client.Connected)
					{
						try
						{
							var pack = TranslationProtocoll.ReadPacket(stream);
							Log($"Got packet #{pack.id} and text {pack.text}");
							pack.method = TranslationProtocoll.PacketMethod.translation;
						

							int size = pack.text.Length * 5;
							var builder = new StringBuilder(size);
							Log(size);
							var str = NativeUtf8FromString(pack.text);
							var o = translate(0, str, size, builder);
							Marshal.FreeHGlobal(str);
							Log($"Translate output: {o}");
							Log("Translation: " + builder.ToString());
							pack.translation = builder.ToString();
							pack.text = null;
							pack.success = true;
							TranslationProtocoll.SendPacket(pack, stream);
						}
						catch (Exception e)
						{
							Log("Got exception in main loop");
							Log(e);
						}
					}
				}
				catch(Exception e)
				{
					Log(e.Message);
				}
			}

			end();
			loader.Dispose();

		}
	}
}