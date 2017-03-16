using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;


namespace CM3D2.AutoTranslate.Plugin
{


	internal static class TranslationProtocoll
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

			[DefaultValue(null)] public int? id = null;
			[DefaultValue(null)] public string text;
			[DefaultValue(null)] public string translation;
			[DefaultValue(null)] public bool? success = null;
		}

		public static bool ParsePacketForTranslation(Packet p, TranslationData d)
		{
			if (p.method != PacketMethod.translation)
			{
				return false;
			}

			d.State = (p.success ?? false) ? TranslationState.Finished : TranslationState.Failed;
			if (d.State != TranslationState.Finished)
				return true;
			d.Translation = p.translation ?? "";
			if (d.Translation == "")
			{
				d.State = TranslationState.Failed;
				return false;
			}
			return true;
		}

		public static void SendPacket(Packet pack, Stream outStream)
		{
			
			var str = JsonFx.Json.JsonWriter.Serialize(pack);
			Logger.Log(str);
			var bytes = Encoding.UTF8.GetBytes(str);

			var size = bytes.Length;
			var netSize = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(size));

			/*outStream.Write(netSize, 0, netSize.Length);
			outStream.Write(bytes, 0, bytes.Length);
			outStream.Flush();*/
			outStream.BeginWrite(netSize, 0, netSize.Length, ar =>
			{
				outStream.EndWrite(ar);
				outStream.BeginWrite(bytes, 0, bytes.Length, br =>
				{
					outStream.EndWrite(br);
				}, null);
			}, null);
		}

	public static void SendTranslationRequest(TranslationData data, Stream outStream)
		{
			Logger.Log($"Sending packet {data.Id}", Level.Debug);
			var pack = new Packet
			{
				method = PacketMethod.translate,
				text = data.Text,
				id = data.Id
			};

			SendPacket(pack, outStream);
		}

		public class WaitForRead : CustomYieldInstruction
		{
			private readonly Stream _stream;
			private bool _finished = false;

			public WaitForRead(Stream stream, byte[] buffer, int offset, int size)
			{
				_stream = stream;
				
				stream.BeginRead(buffer, offset, size, ar =>
				{
					GetReadBytes = _stream.EndRead(ar);
					_finished = true;
				}, this);
				
				//GetReadBytes = _stream.Read(buffer, offset, size);
				//_finished = true;
			}

			public int GetReadBytes { get; private set; }

			public override bool keepWaiting => !_finished;

		}

		public class OutString
		{
			public string data;
			public bool ready = false;
		}

		

		public static IEnumerator ReadJsonObject(Stream inStream, OutString output)
		{
			var sizeBuffBytes = new byte[4];

			var waitSize = new WaitForRead(inStream, sizeBuffBytes, 0, sizeBuffBytes.Length);
			yield return waitSize;

			if (waitSize.GetReadBytes != sizeBuffBytes.Length)
			{
				Logger.Log("Got to few bytes for packet size!", Level.Warn);
				yield break;
			}

			var size = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(sizeBuffBytes, 0));
			Logger.Log($"Got packet size: {size} bytes", Level.Verbose);
			var _buffer = new byte[size];

			var waitPack = new WaitForRead(inStream, _buffer, 0, _buffer.Length);
			yield return waitPack;

			Logger.Log($"Got Packet of size {size}. ", Level.Verbose);

			if (waitPack.GetReadBytes != _buffer.Length)
			{
				Logger.Log($"Got too few bytes for packet! Expected {_buffer.Length}, Got {waitPack.GetReadBytes}", Level.Warn);
				var text = Encoding.UTF8.GetString(_buffer, 0, waitPack.GetReadBytes);
				Logger.LogError($"Only got text {text}");
				yield break;
			}

			output.data = Encoding.UTF8.GetString(_buffer);
			Logger.Log(output.data, Level.Verbose);
			output.ready = true;
		}

		public static IEnumerator ReadPacket(Stream str, Packet pack)
		{
			var output = new OutString();
			yield return ReadJsonObject(str, output);

			var mypack = JsonFx.Json.JsonReader.Deserialize<Packet>(output.data);
			pack.id = mypack.id;
			pack.method = mypack.method;
			if(mypack.translation != null)			
				pack.translation = Uri.UnescapeDataString(mypack.translation);
			if(mypack.text != null)
				pack.text = Uri.UnescapeDataString(mypack.text);
			pack.success = mypack.success;
		}

	}
}
