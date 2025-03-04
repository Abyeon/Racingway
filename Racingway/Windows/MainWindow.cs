using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Numerics;
using Dalamud.Interface.Colors;
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
            new Explore(this.Plugin),
            new Routes(this.Plugin),
            new Records(this.Plugin),
            new Settings(this.Plugin),
            new About(this.Plugin)
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

        using (_ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
        {
            ImGui.TextWrapped("WARNING: It is likely that the way things are saved WILL change." +
                " Meaning that your saved routes and records will be deleted in the future." +
                " If you would like to import a route in the future, save the information via screenshots or otherwise!");
        }

        using (var tabBar = ImRaii.TabBar("##race-tabs", ImGuiTabBarFlags.None))
        {
            if (tabBar)
            {
                for (var i = 0; i < Tabs.Count; i++)
                {
                    var tab = Tabs[i];

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
