using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	internal static class TranslationModuleFactory
	{
		private static readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>();


		static TranslationModuleFactory()
		{
			var baseType = typeof(TranslationModule);
			var curAssembly = Assembly.GetAssembly(baseType);

			foreach (var type in curAssembly.GetTypes())
			{
				if (!type.IsClass || type.IsAbstract || !type.IsSubclassOf(baseType))
					continue;
				var derived = Activator.CreateInstance(type) as TranslationModule;
				if (derived != null)
				{
					_typeMap.Add(derived.Section, type);
				}
			}
		}

		public static TranslationModule Create(string type)
		{
			Type t = null;
			if (!_typeMap.TryGetValue(type, out t))
			{
				return null;
			}

			return Activator.CreateInstance(t) as TranslationModule;
		}
	}
}
