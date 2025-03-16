using Dalamud.Configuration;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Plugin;
using Racingway.Race;
using Racingway.Race.Collision;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Racingway;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AllowDuplicateRecords = true;
    public bool AnnounceLoadedRoutes = true;
    public bool DrawTriggers { get; set; } = false;
    public bool DrawRacingLines { get; set; } = false;
    public bool DrawTimer { get; set; } = false;
    public Vector4 TimerColor { get; set; } = new Vector4(0, 0, 0, 0.75f);
    public Vector4 NormalColor { get; set; } = new Vector4(1, 1, 1, 1);
    public Vector4 FinishColor { get; set; } = new Vector4(0, 1, 1, 1);
    public Vector4 FailColor { get; set; } = new Vector4(1, 0, 0, 1);
    public float TimerSize { get; set; } = 2f;
    public IFontSpec TimerFont { get; set; } = null;
    public bool LogFails { get; set; } = false;
    public bool LogFinish { get; set; } = true;
    public int LineQuality { get; set; } = 10;
    public bool TrackOthers { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
