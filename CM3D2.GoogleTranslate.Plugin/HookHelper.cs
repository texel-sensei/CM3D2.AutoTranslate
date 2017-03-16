using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CM3D2.AutoTranslate.Plugin
{
	internal static class HookHelper
	{
		public enum ParentTranslationPlugin
		{
			None,
			UnifiedTranslationLoader,
			TranslationPlus
		}

		private class PluginHookInfo
		{
			public string AssemblyID;
			public string HookClassName;
			public string OriginalMethodName;
			public string EventName;
			public string PluginClassName;
			public string HookHint;
		}

		private static Dictionary<ParentTranslationPlugin, PluginHookInfo> _pluginHookInfos = new Dictionary<ParentTranslationPlugin, PluginHookInfo>()
		{
			{ParentTranslationPlugin.TranslationPlus, new PluginHookInfo()
			{
				AssemblyID =  ".TranslationPlus.",
				HookClassName = "CM3D2.TranslationPlus.Hook.TranslationPlusHooks",
				PluginClassName = "CM3D2.TranslationPlus.Plugin.TranslationPlus",
				OriginalMethodName = "OnTranslateString",
				EventName = "TranslateText",
				HookHint =  "Hook"
			} },
			{ParentTranslationPlugin.UnifiedTranslationLoader, new PluginHookInfo()
			{
				AssemblyID = ".Translation",
				HookClassName = "CM3D2.Translation.Core",
				PluginClassName = "CM3D2.Translation.Plugin.TranslationPlugin",
				OriginalMethodName = "OnTranslateString",
				EventName = "TranslateText",
				HookHint = "CM3D2.Translation,"
			} }
		};

		private class Hook
		{
			public PluginHookInfo Info;
			public MethodInfo originalTranslationMethod;
			public UnityEngine.Object Plugin;
		}

		public static ParentTranslationPlugin TranslationPlugin { get; private set; } = ParentTranslationPlugin.None;
		private static readonly Hook _hook = new Hook();


		public static void DetectTranslationPlugin()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var longestId = 0;
			foreach (var assembly in assemblies)
			{
				var assemblyName = assembly.GetName();
				Logger.Log($"Checking assembly {assemblyName.FullName} for translation plugin.", Level.Verbose);
				foreach (var pluginHookInfo in _pluginHookInfos)
				{
					var id = pluginHookInfo.Value.AssemblyID;
					if (!assemblyName.FullName.Contains(id)) continue;
					var t = pluginHookInfo.Key;
					
					if (id.Length > longestId)
					{
						Logger.Log($"Found {t} (\"{id}\")!");
						TranslationPlugin = t;
						_hook.Info = pluginHookInfo.Value;
						longestId = id.Length;
					}
					else
					{
						Logger.Log($"Rejecting {t}, has a shorter id (\"{id}\").");
					}
				}
			}
		}

		private static Type FindType(PluginHookInfo info, string extra, bool loadHook)
		{
			var name = loadHook ? info.HookClassName : info.PluginClassName;

			Logger.Log($"Looking for {name} in {info.AssemblyID}, {extra}", Level.Debug);

			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies)
			{
				var assemblyName = assembly.GetName();
				Logger.Log($"Checking {assemblyName}", Level.Verbose);
				if (!assemblyName.FullName.Contains(info.AssemblyID) || !assemblyName.FullName.Contains(extra)) continue;

				Logger.Log($"Found it in {assemblyName.Name}");
				return assembly.GetType(name);
			}
			throw new TypeLoadException("Couldn't find Assembly!");
		}

		public static void HookTranslationEvent(object sender, MethodInfo methodInfo)
		{
			var info = _hook.Info;

			// Load Event
			var type = FindType(info, info.HookHint, true);
			var eventInfo = type.GetEvent(info.EventName);

			// Remove all existing event handlers
			var fieldInfo = type.GetField(info.EventName, BindingFlags.NonPublic | BindingFlags.Static);
			var del = fieldInfo.GetValue(null) as Delegate;
			foreach (var h in del.GetInvocationList())
			{
				eventInfo.RemoveEventHandler(null, h);
			}

			//eventInfo.RemoveEventHandler(null, del);

			// Create and add delegate for own handler
			var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, sender, methodInfo);
			eventInfo.AddEventHandler(null, handler);

			// find original translation method
			var pluginType = FindType(info, "Plugin", false);
			_hook.originalTranslationMethod = pluginType.GetMethod(info.OriginalMethodName,
				BindingFlags.Instance | BindingFlags.NonPublic);

			//var oldHandler = Delegate.CreateDelegate(eventInfo.EventHandlerType, sender, _hook.originalTranslationMethod);
			//eventInfo.RemoveEventHandler(null, oldHandler);

			_hook.Plugin = UnityEngine.Object.FindObjectOfType(pluginType);
			if (_hook.Plugin == null)
			{
				Logger.LogError("Failed to find PluginObject!");	
			}
		}

		public static string CallOriginalTranslator(object sender, object message)
		{
			var res = _hook.originalTranslationMethod.Invoke(_hook.Plugin, new[] {sender, message});
			return res as string;
		}

		public static string GetTextFromEvent(object eventArgs)
		{
			return eventArgs.GetType().GetProperty("Text").GetValue(eventArgs, null) as string;
		}
	}
}
