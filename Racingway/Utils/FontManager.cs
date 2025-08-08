using Dalamud.Interface.ManagedFontAtlas;
using System;

namespace Racingway.Utils
{
    // https://github.com/NightmareXIV/XIVInstantMessenger/blob/master/Messenger/FontControl/FontManager.cs
    public unsafe class FontManager : IDisposable
    {
        private Plugin Plugin;
        public IFontHandle? Handle = null;

        public FontManager(Plugin plugin)
        {
            Plugin = plugin;

            if (Plugin.Configuration.TimerFont != null)
            {
                try
                {
                    Handle = Plugin.Configuration.TimerFont.CreateFontHandle(Plugin.PluginInterface.UiBuilder.FontAtlas);
                } catch (Exception e)
                {
                    Plugin.Configuration.TimerFont = null;
                    Plugin.Log.Error(e.ToString());
                }
            }
        }

        public void Dispose()
        {
            Handle?.Dispose();
        }

        public bool FontPushed = false;
        public bool FontReady => Handle.Available;

        public void PushFont()
        {
            if (FontPushed)
            {
                throw new InvalidOperationException("Font is already pushed.");
            }
            if (Plugin.Configuration.TimerFont != null)
            {
                if (Handle != null && Handle.Available)
                {
                    Handle.Push();
                    FontPushed = true;
                }
            }
        }

        public void PopFont()
        {
            if (FontPushed)
            {
                Handle.Pop();
                FontPushed = false;
            }
        }
    }
}
