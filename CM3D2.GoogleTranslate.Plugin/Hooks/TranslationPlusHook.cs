using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CM3D2.TranslationPlus.Plugin;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin.Hooks
{
    internal class TranslationPlusHook : TranslationPluginHook
    {
        public TranslationPlusHook(AutoTranslatePlugin atp) : base(atp)
        {
        }

        public override Type PluginType => typeof(TranslationPlus.Plugin.TranslationPlus);

        public override void RegisterHook(MonoBehaviour plugin)
        {
            throw new NotImplementedException();
        }
    }
}
