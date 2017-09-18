using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CM3D2.AutoTranslate.Plugin.Hooks
{
    internal abstract class TranslationPluginHook
    {
        protected TranslationPluginHook(AutoTranslatePlugin atp)
        {
            TranslatePlugin = atp;
        }

        public abstract Type PluginType { get; }
        public abstract void RegisterHook(MonoBehaviour plugin);

        protected AutoTranslatePlugin TranslatePlugin { get; private set; }
    }
}
