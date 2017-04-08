using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin
{
	class CoroutineEx
	{
		public Coroutine coroutine { get; private set; }
		private object _result;

		public object Result
		{
			get
			{
				if (ex != null) throw ex;
				return _result;
			}
			private set { _result = value; }
		}

		private Exception ex;
		private IEnumerator target;

		public CoroutineEx(MonoBehaviour owner, IEnumerator target)
		{
			this.target = target;
			this.coroutine = owner.StartCoroutine(Run());
		}

		public void Check()
		{
			if (ex != null) throw ex;
		}

		public Exception GetException()
		{
			return ex;
		}

		private IEnumerator Run()
		{
			while (true)
			{
				try
				{
					if (!target.MoveNext())
					{
						break;
					}
					Result = target.Current;
				}
				catch (Exception e)
				{
					ex = e;
					yield break;
				}
				yield return Result;
			}
		}
	}
}