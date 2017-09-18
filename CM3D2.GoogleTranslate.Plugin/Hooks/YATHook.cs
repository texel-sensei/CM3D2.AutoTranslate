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
            if (args.Translation != null && args.Translation != text)
            {
                return;
            }

            if (args.TextContainer == null)
            {
                return;
            }


            if (!AutoTranslatePlugin.should_translate_text(text))
            {
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
            TranslationHooks.TranslateText += HandleText;
        }
    }
}
