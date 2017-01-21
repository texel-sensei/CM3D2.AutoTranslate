using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
			if (data.translation != null)
				data.translation = Uri.EscapeDataString(data.translation);
			if (data.text != null)
				data.text = Uri.EscapeDataString(data.text);

			var str = JsonConvert.SerializeObject(data);
			var bytes = Encoding.ASCII.GetBytes(str);
			//outStream.BeginWrite(bytes, 0, bytes.Length, outStream.EndWrite, null);
			outStream.Write(bytes, 0, bytes.Length);
		}

		private class Buffer
		{
			public byte[] buffer = new byte[1024];
			public int size = 0;

			public void drop(int num)
			{
				if (num >= size)
				{
					size = 0;
					return;
				}
				Array.Copy(buffer, num, buffer, 0, size - num);
				size -= num;
			}

			public void set(byte[] data, int offset, int length)
			{
				Array.Copy(data, offset, buffer, 0, length);
				size = length;
			}
		}

		public static string ReadJsonObject(Stream inStream)
		{

			var builder = new StringBuilder();
			var stackDepth = 0;
			var found_start = false;
			do
			{
				var c = (char)inStream.ReadByte();
				switch (c)
				{
					case '{':
						stackDepth++;
						found_start = true;
						break;
					case '}':
						stackDepth--;
						break;
				}
				builder.Append(c);
			} while (!found_start || stackDepth > 0);
			return builder.ToString();
		}

		public static  Packet ReadPacket(Stream stream)
		{
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
