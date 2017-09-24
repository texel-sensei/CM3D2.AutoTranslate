using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CM3D2.YATranslator.Plugin;
using CM3D2.YATranslator.Hook;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin.Hooks
{
    internal class YATHook : TranslationPluginHook
    {
        public YATHook(AutoTranslatePlugin atp) : base(atp)
        {
        }

        public override Type PluginType => typeof(YATranslator.Plugin.YATranslator);

        private void HandleText(object sender, StringTranslationEventArgs args)
        {
            var text = args.Text;
            Logger.Log($"Text in {text}", Level.Verbose);
            if (args.Translation != null && args.Translation != text)
            {
                if (!TranslatePlugin.ShouldTranslateText(args.Translation))
                {
                    Logger.Log($"Already translated '{args.Translation}'", Level.Verbose);
                    return;
                }
                text = args.Translation;
            }

            if (args.TextContainer == null)
            {
                Logger.Log("No container");
                return;
            }


            if (!TranslatePlugin.ShouldTranslateText(text))
            {
                Logger.Log("Text doesn't need translating", Level.Verbose);
                return;
            }

            var translation = TranslatePlugin.BuildTranslationData(text, args.TextContainer);

            if (translation.State == TranslationState.Finished)
            {
                return;
            }

            TranslatePlugin.StartTranslation(translation);
        }
        
        public override void RegisterHook(MonoBehaviour plugin)
        {
            Logger.Log("Registering YATranslator Hook", Level.Info);
            TranslationHooks.TranslateText += HandleText;
        }
    }
}
