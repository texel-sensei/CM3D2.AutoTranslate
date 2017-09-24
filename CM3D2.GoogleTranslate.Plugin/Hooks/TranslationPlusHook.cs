using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;


using CM3D2.TranslationPlus.Hook;
using CM3D2.TranslationPlus.Plugin;

namespace CM3D2.AutoTranslate.Plugin.Hooks
{
    internal class TranslationPlusHook : TranslationPluginHook
    {
        // Can't have the subclass TranslationPlus.Plugin.TranslationPlus as a
        // member or the loading of the Autotranslate dll fails
        private MonoBehaviour _translationPlus;

        public TranslationPlusHook(AutoTranslatePlugin atp) : base(atp)
        {
        }

        public override Type PluginType => typeof(TranslationPlus.Plugin.TranslationPlus);
        
        private string CallOriginalTranslator(object sender, StringTranslationEventArgs e)
        {
            var type = typeof(TranslationPlus.Plugin.TranslationPlus);
            var str = (string)
                type.GetMethod(
                    "OnTranslateString",
                    BindingFlags.Instance | BindingFlags.NonPublic
                 ).Invoke(
                    _translationPlus,
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
            Logger.Log("Registering Translation Plus Hook", Level.Info);
            _translationPlus = plugin;
            TranslationPlusHooks.TranslateText += HandleText;
        }
    }
}
