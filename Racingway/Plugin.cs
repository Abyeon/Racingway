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
using FFXIVClientStructs.FFXIV.Common.Component.Excel;
using Lumina.Excel.Sheets;
using Lumina.Excel;

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

    public readonly ExcelSheet<ENpcBase> eNpcBases;

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; }
    public Vector3 startBoxMin = new();
    public Vector3 startBoxMax = new();
    public Vector3 endBoxMin = new();
    public Vector3 endBoxMax = new();

    //public List<Player> trackedPlayers = new List<Player>();
    public List<Trigger> triggers = new List<Trigger>();

    public readonly WindowSystem WindowSystem = new("ParkourTimer");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public TriggerOverlay TriggerOverlay { get; init; }

    public Plugin()
    {
        eNpcBases = DataManager.GetExcelSheet<ENpcBase>();
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        TriggerOverlay = new TriggerOverlay(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(TriggerOverlay);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        Framework.Update += OnFrameworkTick;
        //GameNetwork.NetworkMessage += NetworkMessage;

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public Dictionary<uint, Player> trackedPlayers = new();
    public IGameObject[] trackedNPCs;

    private void OnFrameworkTick(IFramework framework)
    {
        trackedNPCs = GetNPCs(ObjectTable);

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

    public IGameObject[] GetNPCs(IEnumerable<IGameObject> gameObjects)
    {
        return gameObjects.Where(obj => obj is INpc).ToArray();
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

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleTriggerUI() => TriggerOverlay.Toggle();
}
