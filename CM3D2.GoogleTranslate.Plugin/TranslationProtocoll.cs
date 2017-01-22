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

			[DefaultValue(null)]
			public int? id = null;
			[DefaultValue(null)]
			public string text;
			[DefaultValue(null)]
			public string translation;
			[DefaultValue(null)]
			public bool? success = null;
		}

		public static bool ParsePacketForTranslation(Packet p, TranslationData d)
		{
			if (p.method != PacketMethod.translation)
			{
				return false;
			}

			d.Success = p.success ?? false;
			if (!d.Success)
				return true;
			d.Translation = p.translation ?? "";
			if (d.Translation == "")
			{
				d.Success = false;
				return false;
			}
			return true;
		}

		public static void SendTranslationRequest(TranslationData data, BufferedStream outStream)
		{
			CoreUtil.Log($"Sending packet {data.Id}", 0);
			var pack = new Packet
			{
				method = PacketMethod.translate,
				text = data.Text,
				id = data.Id
			};

			var str = JsonFx.Json.JsonWriter.Serialize(pack);
			var bytes = Encoding.ASCII.GetBytes(str);
			outStream.Write(bytes, 0, bytes.Length);
		}

		public class WaitForRead : CustomYieldInstruction
		{
			private readonly Stream _stream;
			private bool _finished = false;
			private int _bytesRead;

			public WaitForRead(Stream stream, byte[] buffer, int offset, int size)
			{
				_stream = stream;
				CoreUtil.Log("Begin reading", 0);
				/*
				stream.BeginRead(buffer, offset, size, ar =>
				{
					CoreUtil.Log("somethingsomething", 0);
					_bytesRead = _stream.EndRead(ar);
					_finished = true;
				}, this);
				CoreUtil.Log("after begin read", 0);
				*/
				_bytesRead = _stream.Read(buffer, offset, size);
				_finished = true;
			}

			public int GetReadBytes => _bytesRead;

			public override bool keepWaiting => !_finished;

		}

		public class OutString
		{
			public string data;
			public bool ready = false;
		}

		private static readonly byte[] _buffer = new byte[4096];
		private static int offset = 0;
		private static int size = 0;
		public static IEnumerator ReadJsonObject(BufferedStream inStream, OutString output)
		{
			
			var builder = new StringBuilder();
			var stackDepth = 0;
			var foundStart = false;
			do
			{
				if (size == 0)
				{
					var wait = new WaitForRead(inStream, _buffer, 0, _buffer.Length);
					CoreUtil.Log("Waiting for data...", 0);
					yield return wait;
					size = wait.GetReadBytes;
					offset = 0;
					CoreUtil.Log($"Got {size} bytes", 0);
				}

				for(; offset <size;++offset)
				{
					var c = (char) _buffer[offset];

					switch (c)
					{
						case '{':
							foundStart = true;
							stackDepth++;
							break;
						case '}':
							stackDepth--;
							break;
					}
					builder.Append(c);
					if (foundStart && stackDepth == 0)
						break;
				}
			} while (!foundStart && stackDepth > 0);
			output.data = builder.ToString();
			CoreUtil.Log(output.data, 2);
			output.ready = true;
		}

		public static IEnumerator ReadPacket(BufferedStream str, Packet pack)
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
