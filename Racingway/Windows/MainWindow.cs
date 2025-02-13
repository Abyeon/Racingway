using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using ImGuizmoNET;
using Newtonsoft.Json;
using Racingway.Tabs;
using Racingway.Utils;

namespace Racingway.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin { get; }
    private List<ITab> Tabs { get; }

    public MainWindow(Plugin plugin)
        : base("Race Setup##race")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;

        this.Tabs = [
            new Routes(this.Plugin),
            new Records(this.Plugin),
            new Settings(this.Plugin)
        ];
    }

    public void Dispose()
    {
        foreach (var tab in Tabs)
        {
            tab.Dispose();
        }
    }

    public override void Draw()
    {
        if (Plugin.ClientState == null) return;

        using (var tabBar = ImRaii.TabBar("##race-tabs", ImGuiTabBarFlags.None))
        {
            if (tabBar)
            {
                foreach (var tab in Tabs)
                {
                    using (var child = ImRaii.TabItem(tab.Name))
                    {
                        if (!child.Success) continue;
                        tab.Draw();
                    }
                }
            }
        }
    }
}
