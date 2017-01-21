using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SimpleJSON;
using UnityEngine;


namespace CM3D2.AutoTranslate.Plugin
{
	

	internal static class TranslationProtocoll
	{
		public const string KEY_METHOD = "method";
		public const string KEY_ID = "id";
		public const string KEY_TEXT = "text";
		public const string KEY_TRANSLATION = "translation";
		private const string KEY_SUCCESS = "success";

		public const string METHOD_TRANSLATE = "translate";
		public const string METHOD_TRANSLATION = "translation";

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

		public static void SendTranslationRequest(TranslationData data, BufferedStream outStream)
		{
			var pack = new Packet
			{
				method = PacketMethod.translate,
				text = data.Text,
				id = data.Id
			};

			var str = JsonFx.Json.JsonWriter.Serialize(pack);
			var bytes = Encoding.ASCII.GetBytes(str);
			outStream.BeginWrite(bytes, 0, bytes.Length, outStream.EndWrite, null);
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
				stream.BeginRead(buffer, offset, size, ar =>
				{
					CoreUtil.Log("somethingsomething", 0);
					_bytesRead = _stream.EndRead(ar);
					_finished = true;
				}, this);
				CoreUtil.Log("after begin read", 0);
			}

			public int GetReadBytes => _bytesRead;

			public override bool keepWaiting => !_finished;

		}

		public class OutString
		{
			public string data;
			public bool ready = false;
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
				Array.Copy(buffer, num, buffer, 0, size-num);
				size -= num;
			}

			public void set(byte[] data, int offset, int length)
			{
				Array.Copy(data, offset, buffer,0, length);
				size = length;
			}
		}

		private static Dictionary<Stream, Buffer> _buffers = new Dictionary<Stream, Buffer>();
		public static IEnumerator ReadJsonObject(BufferedStream inStream, OutString output)
		{
			if (!_buffers.ContainsKey(inStream))
			{
				_buffers.Add(inStream, new Buffer());
			}

			var bytebuffer = _buffers[inStream];
			var buffer = new byte[256];
			var builder = new StringBuilder();
			var stackDepth = 0;
			do
			{
				var read = bytebuffer.size;

				if (read == 0)
				{
					var wait = new WaitForRead(inStream, buffer, 0, buffer.Length);
					CoreUtil.Log("Waiting for data...", 0);
					yield return wait;
					read = wait.GetReadBytes;
					CoreUtil.Log($"Got {read} bytes", 0);
					bytebuffer.set(buffer, 0, buffer.Length);
				}

				var str = Encoding.ASCII.GetString(buffer.Take(read).ToArray());
				var wrote = 0;
				foreach (var c in str)
				{
					switch (c)
					{
						case '{':
							stackDepth++;
							break;
						case '}':
							stackDepth--;
							break;
					}
					builder.Append(c);
					wrote++;
				}

				if (read != wrote)
				{
					bytebuffer.drop(wrote);
				}
			} while (stackDepth > 0);
			output.data = builder.ToString();
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
