using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	internal class TranslationQueue
	{
		private readonly Dictionary<UILabel, List<TranslationData>> _requests = new Dictionary<UILabel, List<TranslationData>>();
		private int _count = 0;

		public void AddRequest(TranslationData req)
		{
			if (!_requests.ContainsKey(req.Label))
			{
				_requests[req.Label] = new List<TranslationData>();
			}
#if DEBUG
			if (_requests[req.Label].Contains(req))
			{
				Logger.LogError($"Added request {req.Id} twice to list!");
				return;
			}
#endif
			_requests[req.Label].Add(req);
			_count++;
		}

		public void RemoveRequest(TranslationData request)
		{
			var succ = _requests[request.Label].Remove(request);
			if(!succ)
				Logger.Log($"Failed to remove request #{request.Id} from queue!", Level.Warn);
			else
			{
				_count--;
			}
		}

		public int Count()
		{
			return _count;
		}

		public IEnumerable<TranslationData> GetRequests(int n)
		{
			var pos = new Dictionary<UILabel, int>();
			var i = 0;
			var hasValues = true;
			while (hasValues)
			{
				hasValues = false;
				foreach (var item in _requests)
				{
					if (i == n)
					{
						yield break;
					}

					if (!pos.ContainsKey(item.Key)) pos[item.Key] = 0;
					var list = item.Value;
					var p = pos[item.Key];
					if (p >= list.Count) continue;
					hasValues = true;
					pos[item.Key]++;
					i++;
					yield return list[p];
				}
			}
		}
	}
}
