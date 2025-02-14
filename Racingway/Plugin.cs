using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Racingway.Windows;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using System;
using Dalamud.Game.Network;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Numerics;
using Racingway.Utils;
using Lumina.Excel.Sheets;
using Lumina.Excel;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop.Generated;
using Racingway.Collision;

namespace Racingway;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework {  get; private set; } = null!;
    [PluginService] internal static IGameNetwork GameNetwork { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Racingway");
    public Logic Logic { get; init; }
    public TriggerOverlay TriggerOverlay { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public List<Record> RecordList { get; init; }
    public Record DisplayedRecord { get; set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        foreach(Trigger trigger in Configuration.Triggers)
        {
            Log.Debug(trigger.selectedType.ToString());
        }

        Logic = new Logic(this); // Initiate collision logic
        SubscribeToTriggers();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        TriggerOverlay = new TriggerOverlay(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(TriggerOverlay);

        RecordList = new List<Record>();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Setup your race!"
        });

        Framework.Update += OnFrameworkTick;
        ClientState.TerritoryChanged += OnTerritoryChange;

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public Dictionary<uint, Player> trackedPlayers = new();
    public IGameObject[] trackedNPCs;

    public void SubscribeToTriggers()
    {
        foreach (Trigger trigger in Configuration.Triggers)
        {
            trigger.Entered -= Logic.OnEntered;
            trigger.Left -= Logic.OnLeft;
            trigger.Entered += Logic.OnEntered;
            trigger.Left += Logic.OnLeft;
        }
    }

    private void OnFrameworkTick(IFramework framework)
    {
        // Check if player does not exist anymore
        foreach (var player in trackedPlayers)
        {
            player.Value.lastSeen++;

            // Player no longer exists
            if (player.Value.lastSeen > 120)
            {
                trackedPlayers.Remove(player.Key);
                break;
            }
        }

        // Check for people
        IGameObject[] players = GetPlayers(ObjectTable);
        foreach (var player in players) 
        {
            uint id = player.EntityId;

            if (!trackedPlayers.ContainsKey(id))
            {
                trackedPlayers.Add(id, new Player(id, player, this));
            } else
            {
                if (player.Position != trackedPlayers[id].position)
                {
                    trackedPlayers[id].Moved(player.Position);
                }

                trackedPlayers[id].lastSeen = 0;
            }
        }
    }

    private unsafe void OnTerritoryChange(ushort territory)
    {
        var manager = HousingManager.Instance();
        var ward = manager->GetCurrentWard();
        var currentPlot = manager->GetCurrentPlot();
        var currentIndoorHouseId = manager->GetCurrentIndoorHouseId();
        var isInside = manager->IsInside();
    }

    public IGameObject[] GetPlayers(IEnumerable<IGameObject> gameObjects)
    {
        return gameObjects.Where(obj => obj is IPlayerCharacter).ToArray();
    }

    public IGameObject GetPlayer(IEnumerable<IGameObject> gameObjects, uint actorId)
    {
        return gameObjects.Where(obj => obj is IPlayerCharacter && obj.EntityId == actorId).First();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        TriggerOverlay.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private unsafe void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
        var manager = HousingManager.Instance();
        var ward = manager->GetCurrentWard();
        var currentPlot = manager->GetCurrentPlot();
        var currentIndoorHouseId = manager->GetCurrentIndoorHouseId();
        var isInside = manager->IsInside();

        Log.Debug(currentIndoorHouseId.ToString());

    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleTriggerUI() => TriggerOverlay.Toggle();
}
