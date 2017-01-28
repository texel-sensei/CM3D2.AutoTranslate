using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OfflineTranslator
{
	internal class TranslationProtocoll
	{
		public enum PacketMethod
		{
			translate,
			translation,
			quit
		}

		[System.Serializable]
		internal class Packet
		{
			public PacketMethod method { get; set; }

			[DefaultValue(null)]
			public int? id = null;
			[DefaultValue(null)]
			public string text;
			[DefaultValue(null)]
			public string translation;
			[DefaultValue(null)]
			public bool? success = null;
		}

		public static void SendPacket(Packet data, Stream outStream)
		{
			var str = JsonConvert.SerializeObject(data);
			var bytes = Encoding.UTF8.GetBytes(str);

			var size = bytes.Length;
			var netSize = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(size));

			outStream.Write(netSize, 0, netSize.Length);
			outStream.Write(bytes, 0, bytes.Length);
		}


		public static string ReadJsonObject(Stream inStream)
		{
			var sizeBuffBytes = new byte[4];
			inStream.Read(sizeBuffBytes, 0, sizeBuffBytes.Length);

			var size = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(sizeBuffBytes, 0));
			Program.Log($"Read packetsize: {size}");
			var _buffer = new byte[size];
			inStream.Read(_buffer, 0, _buffer.Length);
			return Encoding.UTF8.GetString(_buffer);
		}

		public static  Packet ReadPacket(Stream stream)
		{
			Program.Log("Begin reading packet");
			var str = ReadJsonObject(stream);

			Program.Log(str);

			var pack = new Packet();
			JsonConvert.PopulateObject(str, pack);

			if (pack.translation != null)
				pack.translation = Uri.UnescapeDataString(pack.translation);
			if (pack.text != null)
				pack.text = Uri.UnescapeDataString(pack.text);

			return pack;
		}
	}
}
