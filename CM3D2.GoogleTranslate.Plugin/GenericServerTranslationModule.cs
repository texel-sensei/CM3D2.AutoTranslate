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
			CoreUtil.Log("Sending Request", 9);
			TranslationProtocoll.SendTranslationRequest(data, _stream);
			CoreUtil.Log("Sent Request", 9);
			var output = new TranslationProtocoll.OutString();

			yield return TranslationProtocoll.ReadJsonObject(_stream, output);
			CoreUtil.Log(output.data, 2);
			var pack = JsonFx.Json.JsonReader.Deserialize<TranslationProtocoll.Packet>(output.data);
			CoreUtil.Log(pack.ToString(), 2);
		}

		public override void DeInit()
		{
			_connection.Close();
		}
	}
}
