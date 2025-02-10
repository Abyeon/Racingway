using Dalamud.Configuration;
using Dalamud.Plugin;
using Racingway.Utils;
using System;
using System.Collections.Generic;

namespace Racingway;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool DrawRacingLines { get; set; } = false;
    public List<Trigger> triggers { get; set; } = new List<Trigger>();

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
