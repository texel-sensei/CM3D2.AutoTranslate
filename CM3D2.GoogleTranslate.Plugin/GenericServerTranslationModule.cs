using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin
{
	class GenericServerTranslationModule : TranslationModule
	{
		public override string Section => "GeneralNetworkTranslation";

		private string _host = "localhost";
		private int _port = 9586;

		private TcpClient _connection;
		private BufferedStream _stream;

		private readonly Dictionary<int, TranslationProtocoll.Packet> _arrivedTranslations = new Dictionary<int, TranslationProtocoll.Packet>();
		private int _openRequests = 0;

		protected override void LoadConfig(CoreUtil.SectionLoader section)
		{
			section.LoadValue("Host", ref _host);
			section.LoadValue("Port", ref _port);
		}

		public override bool Init()
		{
			try
			{
				_connection = new TcpClient(_host, _port);
				_stream = new BufferedStream(_connection.GetStream());
			}
			catch (Exception e)
			{
				CoreUtil.LogError(e.Message);
				return false;
			}
			return true;
		}

		private IEnumerator CollectPackets()
		{
			while (_openRequests > 0)
			{
				var pack = new TranslationProtocoll.Packet();
				yield return TranslationProtocoll.ReadPacket(_stream, pack);
				CoreUtil.Log($"Got data! Packet #{pack.id}", 0);
				CoreUtil.Log(pack.text, 0);
				if (pack.method == TranslationProtocoll.PacketMethod.translation)
				{
					_openRequests--;
				}
				if(pack.id != null)
					_arrivedTranslations.Add(pack.id.Value, pack);
			}
			yield break;
		}

		public override IEnumerator Translate(TranslationData data)
		{
			bool startCollecting = _openRequests == 0;
			_openRequests++;
			TranslationProtocoll.SendTranslationRequest(data, _stream);

			if (startCollecting)
			{
				StartCoroutine(CollectPackets());
			}

			yield return new WaitForTranslation(this, data.Id);

			var pack = _arrivedTranslations[data.Id];
			TranslationProtocoll.ParsePacketForTranslation(pack, data);
		}

		internal class WaitForTranslation : CustomYieldInstruction
		{
			private readonly GenericServerTranslationModule t;
			private readonly int _id;

			public WaitForTranslation(GenericServerTranslationModule t, int id)
			{
				_id = id;
				this.t = t;
			}

			public override bool keepWaiting => !t._arrivedTranslations.ContainsKey(_id);
		}

		public override void DeInit()
		{
			_connection.Close();
		}
	}
}
