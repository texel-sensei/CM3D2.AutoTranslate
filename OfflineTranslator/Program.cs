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
		public static void Log(object msg)
		{
			Debug.WriteLine(msg);
			Console.WriteLine(msg);
		}

		private static void Main(string[] args)
		{
			var server = new TcpListener(IPAddress.Any, 9586);
			server.Start();


			using (var translator = new LECWrapper())
			{
				translator.Init();
				while (true)
				{
					try
					{
						Log("listening!");

						var client = server.AcceptTcpClient();
						var stream = client.GetStream();

						Log("Someone connected!");
						while (client.Connected)
						{
							try
							{
								var pack = TranslationProtocoll.ReadPacket(stream);
								Log($"Got packet #{pack.id} and text {pack.text}");
								if (pack.method == TranslationProtocoll.PacketMethod.quit)
								{
									Log("Client quit.");
									break;
								}
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
					catch (Exception e)
					{
						Log(e.Message);
					}
				}
			}
		}
	}
}