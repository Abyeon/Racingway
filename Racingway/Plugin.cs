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
using LiteDB;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonJobHudRDM0.BalanceGauge;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Racingway;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
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

    public TriggerOverlay TriggerOverlay { get; init; }
    private MainWindow MainWindow { get; init; }

    public List<Record> RecordList { get; set; } = new();
    public List<Route> LoadedRoutes { get; set; } = new();

    public ObjectId? DisplayedRecord { get; set; }
    public ObjectId? SelectedRoute { get; set; }
    public Stopwatch LocalTimer { get; set; }

    public Address CurrentAddress { get; set; }

    public Plugin()
    {
        try
        {
            LocalTimer = new Stopwatch();
            territoryHelper = new TerritoryHelper(this);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            try
            {
                Storage = new(this, $"{PluginInterface.GetPluginConfigDirectory()}\\data.db");

                // Delete current user's database if they are still using a legacy version

                if (Configuration.Version == 0)
                {
                    Storage.GetRecords().DeleteAll();
                    Storage.GetRoutes().DeleteAll();

                    Plugin.ChatGui.PrintError($"[RACE] Due to changes in the database, Racingway has wiped the current one.");

                    Configuration.Version = 1;
                    Configuration.Save();
                }

                Storage.UpdateRouteCache();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            MainWindow = new MainWindow(this);
            TriggerOverlay = new TriggerOverlay(this);

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(TriggerOverlay);

            RecordList = new List<Record>();
            LoadedRoutes = new List<Route>();

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Setup your race!"
            });

            Framework.Update += OnFrameworkTick;
            ClientState.TerritoryChanged += OnTerritoryChange;

            PluginInterface.UiBuilder.Draw += DrawUI;

            // This adds a button to the plugin installer entry of this plugin which allows
            // to toggle the display status of the main ui
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            // Enable overlay if config calls for it
            ShowHideOverlay();

            // Update our address when plugin first loads
            Parallel.Invoke(() => territoryHelper.GetLocationID());
        } catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
        
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

    public List<(Func<bool>, Stopwatch)> polls = new();

    public void CheckCollision(Player player)
    {
        if (LoadedRoutes == null || LoadedRoutes.Count == 0) return;

        foreach(Route route in LoadedRoutes)
        {
            route.CheckCollision(player);
        }
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!ClientState.IsLoggedIn || ClientState.IsPvP) return;

        if (polls != null && polls.Count > 0)
        {
            List<(Func<bool>, Stopwatch)> toRemove = new();

            // Loop through requested polling tasks
            foreach (var poll in polls)
            {
                try
                {
                    bool result = poll.Item1.Invoke();

                    if (result == true || poll.Item2.ElapsedMilliseconds > 1000) toRemove.Add(poll);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }

            // Delete polling tasks that have completed
            foreach (var poll in toRemove)
            {
                try
                {
                    polls.Remove(poll);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
        }

        // If we even have routes loaded, then we can track players
        if (LoadedRoutes.Count > 0)
        {
            try
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

                if (Configuration.TrackOthers)
                {
                    // Check for people
                    ICharacter[] players = GetPlayers(ObjectTable);
                    foreach (var player in players)
                    {
                        uint id = player.EntityId;

                        if (!trackedPlayers.ContainsKey(id))
                        {
                            trackedPlayers.Add(id, new Player(id, player, this));
                        }
                        else
                        {
                            trackedPlayers[id].actor = player;

                            bool lastGrounded = trackedPlayers[id].isGrounded;
                            trackedPlayers[id].UpdateState();

                            if (player.Position != trackedPlayers[id].position || lastGrounded != trackedPlayers[id].isGrounded)
                            {
                                trackedPlayers[id].Moved(player.Position);
                            }

                            trackedPlayers[id].lastSeen = 0;
                        }
                    }
                }
                else
                {
                    // Copied from above but modified to track just the client. A tad stupid.
                    // Check for people
                    ICharacter player = GetPlayers(ObjectTable).FirstOrDefault(x => x.EntityId == ClientState.LocalPlayer.EntityId, null);
                    if (player != null)
                    {
                        uint id = player.EntityId;

                        if (!trackedPlayers.ContainsKey(id))
                        {
                            trackedPlayers.Add(id, new Player(id, player, this));
                        }
                        else
                        {
                            trackedPlayers[id].actor = player;

                            bool lastGrounded = trackedPlayers[id].isGrounded;
                            trackedPlayers[id].UpdateState();

                            if (player.Position != trackedPlayers[id].position || lastGrounded != trackedPlayers[id].isGrounded)
                            {
                                trackedPlayers[id].Moved(player.Position);
                            }

                            trackedPlayers[id].lastSeen = 0;
                        }
                    }
                }
            } catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }
    }

    private void OnTerritoryChange(ushort territory)
    {
        trackedPlayers.Clear();

        try
        {
            Parallel.Invoke(() => territoryHelper.GetLocationID());
        } catch (Exception e)
        {
            Plugin.Log.Error(e.ToString());
        }
    }

    // Triggered whenever TerritoryHelper learns the ID of the location we're at
    public void AddressChanged(Address address)
    {
        ChatGui.Print(address.ReadableName);

        CurrentAddress = address;
        LoadedRoutes.Clear();
        RecordList.Clear();

        try
        {
            List<Route> addressRoutes = Storage.GetRoutes().Query().Where(r => r.Address.LocationId == address.LocationId).ToList();
            LoadedRoutes = addressRoutes;

            // Update route addresses to address legacy routes
            foreach (Route route in addressRoutes)
            {
                if (route.Address == null || route.Address != address)
                {
                    route.Address = address;
                    Storage.AddRoute(route);
                }
            }

            RecordList = Storage.GetRecords().Query().Where(r => r.RouteAddress == CurrentAddress.LocationId).ToList();
            DisplayedRecord = null;

            // Kick everyone from parkour when you change zones
            foreach (var player in trackedPlayers)
            {
                player.Value.inParkour = false;
                player.Value.raceLine.Clear();
            }

            if (addressRoutes.Count() > 0 && Configuration.AnnounceLoadedRoutes)
            {
                ChatGui.Print($"[RACE] Loaded {addressRoutes.Count()} routes in this area.");
            }

            if (LoadedRoutes.Count > 0)
                SelectedRoute = LoadedRoutes.First().Id;

        } catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
        

        SubscribeToRouteEvents();
    }

    public void SubscribeToRouteEvents()
    {
        foreach (var route in LoadedRoutes)
        {
            route.OnStarted -= OnStart;
            route.OnFinished -= OnFinish;
            route.OnFailed -= OnFailed;
            route.OnStarted += OnStart;
            route.OnFinished += OnFinish;
            route.OnFailed += OnFailed;
        }
    }

    public void UnsubscribeFromRouteEvents()
    {
        foreach (var route in LoadedRoutes)
        {
            route.OnStarted -= OnStart;
            route.OnFinished -= OnFinish;
            route.OnFailed -= OnFailed;
        }
    }

    private void OnStart(object? sender, Player e)
    {
        if (e.id == ClientState.LocalPlayer.EntityId)
        {
            LocalTimer.Reset();
            LocalTimer.Start();
        }
    }

    private void OnFinish(object? sender, (Player, Record) e)
    {
        if (e.Item1.actor.EntityId == ClientState.LocalPlayer.EntityId)
        {
            LocalTimer.Stop();
        }

        var prettyPrint = Time.PrettyFormatTimeSpan(e.Item2.Time);

        Route? route = sender as Route;

        if (route == null)
        {
            Plugin.ChatGui.PrintError("[RACE] Route is null.");
            return;
        }

        if (Configuration.LogFinish)
        {
            PayloadedChat((IPlayerCharacter)e.Item1.actor, $" just finished {route.Name} in {prettyPrint} and {e.Item2.Distance} units.");
        }

        RecordList.Add(e.Item2 as Record);
        Storage.AddRecord(e.Item2 as Record);

        if (route.BestRecord == null || e.Item2.Time.TotalNanoseconds < route.BestRecord.Time.TotalNanoseconds)
        {
            route.BestRecord = e.Item2;
        }

        Storage.RouteCache[route.Id.ToString()] = route;
    }

    private void OnFailed(object? sender, Player e)
    {
        if (e.actor.EntityId == ClientState.LocalPlayer.EntityId)
        {
            LocalTimer.Reset();
        }

        if (Configuration.LogFails)
        {
            PayloadedChat((IPlayerCharacter)e.actor, " just failed the parkour.");
        }
    }

    public void PayloadedChat(IPlayerCharacter player, string message)
    {
        PlayerPayload payload = new PlayerPayload(player.Name.ToString(), player.HomeWorld.Value.RowId);
        TextPayload text = new TextPayload(message);
        SeString chat = new SeString(new Payload[] { payload, text });

        Plugin.ChatGui.Print(chat);
    }

    public ICharacter[] GetPlayers(IEnumerable<IGameObject> gameObjects)
    {
        IGameObject[] objects = gameObjects.Where(obj => obj is IPlayerCharacter).ToArray();
        ICharacter[] players = objects.Cast<ICharacter>().ToArray();
        return players;
    }

    public IPlayerCharacter GetPlayer(IEnumerable<IGameObject> gameObjects, uint actorId)
    {
        return (IPlayerCharacter)gameObjects.Where(obj => obj is IPlayerCharacter && obj.EntityId == actorId).First();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        UnsubscribeFromRouteEvents();
        LoadedRoutes.Clear();
        RecordList.Clear();

        LocalTimer.Stop();

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

        //await territoryHelper.GetLocationID(0);
        //Log.Debug(Storage.GetRecords().FindOne(x => x.Id == DisplayedRecord).Line.Length.ToString());
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleConfigUI() => MainWindow.Toggle();
    public void ToggleTriggerUI() => TriggerOverlay.Toggle();
}
