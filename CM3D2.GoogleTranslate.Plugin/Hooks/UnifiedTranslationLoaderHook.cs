using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

using CM3D2.Translation;

namespace CM3D2.AutoTranslate.Plugin.Hooks
{
    internal class UnifiedTranslationLoaderHook : TranslationPluginHook
    {
        public override Type PluginType => typeof(Translation.Plugin.TranslationPlugin);


        // Can't have the subclass Translation.Plugin.TranslationPlugin as a
        // member or the loading of the Autotranslate dll fails
        private MonoBehaviour _unifiedTranslationLoader;

        public UnifiedTranslationLoaderHook(AutoTranslatePlugin atp) : base(atp)
        {
        }

        private string CallOriginalTranslator(object sender, StringTranslationEventArgs e)
        {
            var type = typeof(Translation.Plugin.TranslationPlugin);
            var str = (string)
                type.GetMethod(
                    "OnTranslateString",
                    BindingFlags.Instance | BindingFlags.NonPublic
                 ).Invoke(
                    _unifiedTranslationLoader,
                    new object[]{sender,e}
                );
            return str;
        }

        private string HandleText(object sender, StringTranslationEventArgs e)
        {
            var txt = e.Text;
            Logger.Log($"Text in '{txt}'", Level.Verbose);
            if (!TranslatePlugin.ShouldTranslateText(txt))
            {
                Logger.Log("Doesn't need translating, ignoring it!", Level.Verbose);
                return txt;
            }

            var ft = CallOriginalTranslator(sender, e);
            if (
                ft != null && ft.Trim().Length > 0 
                && AutoTranslatePlugin.IsAsciiText(ft))
            {
                Logger.Log("Already translated by TP", Level.Verbose);
                return ft;
            }

            var trans = TranslatePlugin.BuildTranslationData(txt, sender as MonoBehaviour);

            if (trans.State == TranslationState.Finished)
            {
                return trans.Translation;
            }

            TranslatePlugin.StartTranslation(trans);
            return null;
        }

        public override void RegisterHook(MonoBehaviour plugin)
        {
            Logger.Log("Registering Unified Translation Loader Hook", Level.Info);
            _unifiedTranslationLoader = plugin as Translation.Plugin.TranslationPlugin;
            Translation.Core.TranslateText += HandleText;
        }
    }
}
