using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

			Log("starting listening!");

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
		}
	}
}