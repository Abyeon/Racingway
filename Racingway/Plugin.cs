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

namespace Racingway;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameNetwork GameNetwork { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; }
    public Vector3 startBoxMin = new();
    public Vector3 startBoxMax = new();
    public Vector3 endBoxMin = new();
    public Vector3 endBoxMax = new();

    public List<Player> trackedPlayers = new List<Player>();
    public List<Trigger> triggers = new List<Trigger>();

    public readonly WindowSystem WindowSystem = new("ParkourTimer");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private TriggerOverlay TriggerOverlay { get; init; }

    public Plugin()
    {
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

        GameNetwork.NetworkMessage += NetworkMessage;

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    private void NetworkMessage(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
    {
        // 988 is client moved
        // 938 is actor moved
        if (ClientState.IsLoggedIn && (opCode == 938 ||  opCode == 988))
        {
            uint id = targetActorId;

            // If packet is coming from local player, change ID to match
            if (id == 0 && ClientState.LocalPlayer != null)
            {
                id = ClientState.LocalPlayer.EntityId;
            }

            IGameObject player = GetPlayer(ObjectTable, id);

            // Player is not tracked
            if (!trackedPlayers.Any(x=>x.id == id))
            {
                trackedPlayers.Add(new Player(id, player, this));
            }

            trackedPlayers.Find(x => x.id == id).Moved(player.Position);
        }
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
