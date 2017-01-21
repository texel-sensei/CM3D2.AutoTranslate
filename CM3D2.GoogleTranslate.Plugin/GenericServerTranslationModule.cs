using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	class GenericServerTranslationModule : TranslationModule
	{
		protected override string Section => "GenericNetworkTranslation";

		private string _host = "localhost";
		private int _port = 9586;

		private TcpClient _connection;
		private BufferedStream _stream;

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

		public override IEnumerator Translate(TranslationData data)
		{
			TranslationProtocoll.SendTranslationRequest(data, _stream);
			var pack = new TranslationProtocoll.Packet();

			yield return TranslationProtocoll.ReadPacket(_stream, pack);

			CoreUtil.Log($"Got data! Packet #{pack.id}", 0);
			if (pack.id != data.Id)
			{
				CoreUtil.LogError($"Packet swap detected! {pack.id} <-> {data.Id}");
			}
			CoreUtil.Log(pack.text, 0);
			data.Success = pack.success ?? false;
			data.Translation = pack.translation ?? "";
			if (data.Translation == "") data.Success = false;
		}

		public override void DeInit()
		{
			_connection.Close();
		}
	}
}
