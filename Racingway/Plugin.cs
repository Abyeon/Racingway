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
using LiteDB;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonJobHudRDM0.BalanceGauge;
using System.Threading.Tasks;
using System.Collections.Concurrent;

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

    internal LocalDatabase Storage { get; init; }
    internal TerritoryHelper territoryHelper { get; set; }

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Racingway");
    public Logic Logic { get; init; }
    public TriggerOverlay TriggerOverlay { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public List<Record> RecordList { get; init; }
    public ObjectId DisplayedRecord { get; set; }
    public long CurrentTerritory = 0;

    public Plugin()
    {
        try 
        {
            Storage = new(this, $"{PluginInterface.GetPluginConfigDirectory()}\\data.db");
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
        }

        territoryHelper = new TerritoryHelper(this);

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

        // Enable overlay if config calls for it
        ShowHideOverlay();
    }

    public Dictionary<uint, Player> trackedPlayers = new();
    public IGameObject[] trackedNPCs;

    public void ShowHideOverlay()
    {
        if (Configuration.DrawRacingLines || Configuration.DrawTriggers || DisplayedRecord != null)
        {
            TriggerOverlay.IsOpen = true;
        } else
        {
            TriggerOverlay.IsOpen = false;
        }
    }

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

    public void UnsubscribeFromTriggers()
    {
        foreach (Trigger trigger in Configuration.Triggers)
        {
            trigger.Entered -= Logic.OnEntered;
            trigger.Left -= Logic.OnLeft;
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

    private async void OnTerritoryChange(ushort territory)
    {
        // Honestly, this is not safe. A crash will happen if somebody.. quits their game within 3 seconds.
        // Obviously that's a little silly, but its not cool to see a crash pop up.
        Log.Debug(territory.ToString());
        Task task = new Task(async () =>
        {
            CurrentTerritory = await territoryHelper.GetLocationID(territory);
            Plugin.ChatGui.Print(CurrentTerritory.ToString());
        });
        task.Start();
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
        UnsubscribeFromTriggers();

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        TriggerOverlay.Dispose();

        Storage.Dispose();
        Configuration.Save();

        CommandManager.RemoveHandler(CommandName);
    }

    private async void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();

        await territoryHelper.GetLocationID(0);
        //Log.Debug(Storage.GetRecords().FindOne(x => x.Id == DisplayedRecord).Line.Length.ToString());

    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleTriggerUI() => TriggerOverlay.Toggle();
}
