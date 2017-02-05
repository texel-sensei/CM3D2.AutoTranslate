using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OfflineTranslator
{




	internal class Program
	{
		public const string Version = "v0.2";
		private static int _port = 9586;

		public static void Log(object msg)
		{
			Debug.WriteLine(msg);
			Console.WriteLine(msg);
		}

		private static void DoMessageLoop(DllWrapper translator, TcpListener server)
		{
			Log("Waiting for connection...");
			var client = server.AcceptTcpClient();
			var stream = client.GetStream();

			Log("Got connection!");
			while (client.Connected)
			{
				try
				{
					var pack = TranslationProtocoll.ReadPacket(stream);

					if (pack.method == TranslationProtocoll.PacketMethod.quit)
					{
						Log("Client quit.");
						break;
					}
					Log($"Got packet #{pack.id} and text {pack.text}");
					pack.method = TranslationProtocoll.PacketMethod.translation;

					var translation = translator.Translate(pack.text);
					Log($"Translation for #{pack.id} is {translation}");
					pack.translation = translation;
					pack.text = null;
					pack.success = translation != null;
					//Thread.Sleep(2000);
					TranslationProtocoll.SendPacket(pack, stream);
				}
				catch (Exception e)
				{
					Log("Got exception in main loop");
					Log(e);
				}
			}
		}

		private static void Main(string[] args)
		{
			
			Log($"Translation Server {Version}");

			try
			{
				using (var translator = new LECWrapper())
				{
					Log("Loading LEC...");
					var succ = translator.Init();

					if (!succ)
					{
						Log("Failed to load LEC!");
					}
					else
					{
						Log($"Successfully loaded LEC at '{translator.FullDllPath}'");

						Log($"Listening on port {_port}");
						var server = new TcpListener(IPAddress.Any, _port);
						server.Start();

						DoMessageLoop(translator, server);
					}	
				}
			}
			catch (Exception e)
			{
				Log("Got exception!");
				Log(e);
			}

			Console.WriteLine("Press any key to continue . . .");
			Console.ReadKey();
		}
		
	}
}