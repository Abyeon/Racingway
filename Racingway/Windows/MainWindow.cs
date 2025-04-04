using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Racingway.Tabs;

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

    private string? SelectedTab { get; set; }

    public void SelectTab(string label)
    {
        SelectedTab = label;
    }

    // Thanks to Asriel:
    //https://github.com/WorkingRobot/Waitingway/blob/5b97266c2f68f8a6f38d19e1d9a0337973254264/Waitingway/Windows/Settings.cs#L75
    private ImRaii.IEndObject TabItem(string label)
    {
        var isSelected = string.Equals(SelectedTab, label, StringComparison.Ordinal);
        if (isSelected)
        {
            SelectedTab = null;
            var open = true;
            return ImRaii.TabItem(label, ref open, ImGuiTabItemFlags.SetSelected);
        }
        return ImRaii.TabItem(label);
    }

    public override void Draw()
    {
        if (Plugin.ClientState == null) return;

        using (var tabBar = ImRaii.TabBar("##race-tabs", ImGuiTabBarFlags.None))
        {
            if (tabBar)
            {
                for (var i = 0; i < Tabs.Count; i++)
                {
                    var tab = Tabs[i];
                    
                    //var isSelected = string.Equals(SelectedTab, tab.Name, StringComparison.Ordinal);
                    //if (isSelected)
                    //{
                    //    SelectedTab = null;
                    //    var open = true;
                    //    ImGui.BeginTabItem(tab.Name, ref open, ImGuiTabItemFlags.SetSelected);
                    //} else
                    //{
                    //    ImGui.BeginTabItem(tab.Name);
                    //}

                    //// Doing this so that if a tab requires a scrollbar, the tabbar stays on top.
                    //using (var tabChild = ImRaii.Child($"###{tab.Name}-child"))
                    //{
                    //    if (!tabChild.Success) continue;
                    //    tab.Draw();
                    //}

                    //ImGui.EndTabItem();

                    using (var child = TabItem(tab.Name))
                    {
                        if (!child.Success) continue;

                        // Doing this so that if a tab requires a scrollbar, the tabbar stays on top.
                        using (var tabChild = ImRaii.Child($"###{tab.Name}-child"))
                        {
                            if (!tabChild.Success) continue;
                            tab.Draw();
                        }
                    }
                }
            }
        }
    }
}
