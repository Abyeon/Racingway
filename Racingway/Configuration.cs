using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Plugin;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Tabs;

namespace Racingway;

[Serializable]
public class Configuration : IPluginConfiguration
{
    /// <see cref="Racingway.Tabs.Settings"/>

    public int Version { get; set; } = 1;

    public bool AllowDuplicateRecords = true;
    public bool AnnounceLoadedRoutes = true;

    // Display Toggles
    public bool DrawTriggers { get; set; } = false;
    public bool DrawRacingLines { get; set; } = false;
    public bool DrawTimer { get; set; } = false;
    public bool DrawTimerButtons {  get; set; } = true;

    // Timer Style
    public float TimerSize { get; set; } = 2f;
    public IFontSpec TimerFont { get; set; } = null;
    public Vector4 TimerColor { get; set; } = new Vector4(0, 0, 0, 0.75f);
    public Vector4 NormalColor { get; set; } = new Vector4(1, 1, 1, 1);
    public Vector4 FinishColor { get; set; } = new Vector4(0, 1, 1, 1);
    public Vector4 FailColor { get; set; } = new Vector4(1, 0, 0, 1);

    // Timer Behavior
    public bool ShowWhenInParkour { get; set; } = true;
    public int SecondsShownAfter { get; set; } = 2;

    // Chat output
    public bool LogStart { get; set; } = false;
    public bool LogFails { get; set; } = false;
    public bool LogFinish { get; set; } = true;

    // Line Style
    public int LineQuality { get; set; } = 10;
    public string LineStyle { get; set; } = "Line";
    public float DotSize { get; set; } = 3.0f;
    public float LineThickness { get; set; } = 2.0f;
    public Vector4 LineColor { get; set; } = new Vector4(1f, 1f, 1f, 0.33f); // 0x55FFFFFF hopefully
    public Vector4 HighlightedLineColor { get; set; } = new Vector4(1f, 0.8f, 1f, 0.33f); // 0x55FFCCFF hopefully
    public int MaxLinePoints { get; set; } = 500; // Control memory usage and performance

    // Misc Settings
    public bool TrackOthers { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
