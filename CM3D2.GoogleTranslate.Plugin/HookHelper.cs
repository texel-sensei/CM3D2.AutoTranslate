using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using CM3D2.AutoTranslate.Plugin.Hooks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CM3D2.AutoTranslate.Plugin
{
	internal static class HookHelper
	{
	    
        public enum ParentTranslationPlugin
		{
			None,
			UnifiedTranslationLoader,
			TranslationPlus,
			YetAnotherTranslator
		}

		private class PluginHookInfo
		{
			public string AssemblyID;
			public string EventName;
		    public string HandlerModuleName;
		}

		private static readonly Dictionary<ParentTranslationPlugin, PluginHookInfo> _pluginHookInfos = new Dictionary<ParentTranslationPlugin, PluginHookInfo>()
		{
			{ParentTranslationPlugin.TranslationPlus, new PluginHookInfo()
			{
				AssemblyID =  ".TranslationPlus.",
				EventName = "TranslateText",
                HandlerModuleName = "TranslationPlusHook"
			} },
			{ParentTranslationPlugin.UnifiedTranslationLoader, new PluginHookInfo()
			{
				AssemblyID = ".Translation",
				EventName = "TranslateText",
			    HandlerModuleName = "UTLHook"
            } },
			{ParentTranslationPlugin.YetAnotherTranslator, new PluginHookInfo()
			{
				AssemblyID = ".YATranslator",
				EventName = "TranslateText",
			    HandlerModuleName = "YATHook"
            } }
		};

		private class Hook
		{
			public PluginHookInfo Info;	
		    public TranslationPluginHook PluginHandler;
            public UnityEngine.MonoBehaviour Plugin;
		}


        public static ParentTranslationPlugin TranslationPlugin { get; private set; } = ParentTranslationPlugin.None;
		private static readonly Hook _hook = new Hook();

		public static ParentTranslationPlugin DetectTranslationPlugin()
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
		    return TranslationPlugin;
		}

		public static bool HookTranslationEvent(AutoTranslatePlugin atp)
		{
		    try
		    {
		        var curAssembly = Assembly.GetAssembly(typeof(TranslationPluginHook));
		        var namespace_ = "CM3D2.AutoTranslate.Plugin.Hooks";
		        var hookType = curAssembly.GetType(
		            namespace_ + "." + _hook.Info.HandlerModuleName
		        );

		        var handler =  Activator.CreateInstance(hookType, new object[]{atp}) as TranslationPluginHook;
		        if (handler == null)
		            return false;

		        _hook.PluginHandler = handler;
		        _hook.Plugin = Object.FindObjectOfType(handler.PluginType) as MonoBehaviour;

		        if (_hook.Plugin == null)
		        {
		            Logger.LogError($"Couldn't find {TranslationPlugin} Plugin!");
		        }

                handler.RegisterHook(_hook.Plugin);

		        return true;
		    }
		    catch (Exception e)
		    {
		        Logger.LogError("Got Exception while hooking!", e);
		        return false;
		    }
		}

        /*
         *  This function removes all registered delegates from the event, except for 
         *  our own delegate
         */
        private static void RemoveExisingHandlers(
            PluginHookInfo info, Type type, EventInfo eventInfo
        ){
            var fieldInfo = type.GetField(info.EventName, BindingFlags.NonPublic | BindingFlags.Static);
            var del = fieldInfo.GetValue(null) as Delegate;
            foreach (var h in del.GetInvocationList())
            {
                eventInfo.RemoveEventHandler(null, h);
            }
        }
	}
}
