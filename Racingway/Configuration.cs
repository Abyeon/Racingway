using Dalamud.Configuration;
using Dalamud.Plugin;
using Racingway.Collision;
using System;
using System.Collections.Generic;

namespace Racingway;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool AllowDuplicateRecords = true;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool DrawTriggers { get; set; } = false;
    public bool DrawRacingLines { get; set; } = false;
    public bool LogFails { get; set; } = false;
    public bool LogFinish { get; set; } = true;
    public int LineQuality { get; set; } = 10;
    public List<Trigger> Triggers { get; set; } = new List<Trigger>();

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
