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
		private StreamWriter _stream;

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
				_stream = new StreamWriter(_connection.GetStream());
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
			_stream.WriteLine(data.Text);
			yield break;
		}

		public override void DeInit()
		{
			_stream.Close();
			_connection.Close();
		}
	}
}
