using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Racingway.Windows;
using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Racingway.Utils;
using LiteDB;
using System.Threading.Tasks;
using System.Diagnostics;
using Racingway.Race;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ManagedFontAtlas;
using Racingway.Utils.Storage;
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
    public DataQueue DataQueue { get; init; }

    internal TerritoryHelper territoryHelper { get; set; }

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Racingway");
    public FontManager FontManager { get; init; }

    public TriggerOverlay TriggerOverlay { get; init; }
    private MainWindow MainWindow { get; init; }
    public TimerWindow TimerWindow { get; init; }

    public List<Route> LoadedRoutes { get; set; } = new();

    public Record DisplayedRecord { get; set; }
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
            FontManager = new FontManager(this);

            DataQueue = new DataQueue();

            try
            {
                Storage = new(this, $"{PluginInterface.GetPluginConfigDirectory()}\\data.db");

                //Configuration.Version = 0;
                // Delete current user's database if they are still using a legacy version
                if (Configuration.Version == 0)
                {
                    Storage.GetRecords().DeleteAll();
                    Storage.GetRoutes().DeleteAll();

                    Plugin.ChatGui.PrintError($"[RACE] Due to changes in the database, Racingway has wiped your previous data.. Apologies for this!");

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
            TimerWindow = new TimerWindow(this);

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(TriggerOverlay);
            WindowSystem.AddWindow(TimerWindow);

            LoadedRoutes = new List<Route>();

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the main UI"
            });

            Framework.Update += OnFrameworkTick;
            ClientState.TerritoryChanged += OnTerritoryChange;
            ClientState.Logout += OnLogout;

            PluginInterface.UiBuilder.Draw += DrawUI;

            // This adds a button to the plugin installer entry of this plugin which allows
            // to toggle the display status of the main ui
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            // Enable overlay if config calls for it
            ShowHideOverlay();

            // Update our address when plugin first loads
            territoryHelper.GetLocationID();
        } catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
        
    }

    public Dictionary<uint, Player> trackedPlayers = new();
    public IGameObject[] trackedNPCs;

    public void ShowHideOverlay()
    {
        if (Configuration.DrawTimer)
        {
            TimerWindow.IsOpen = true;
        } else
        {
            if (!(Configuration.ShowWhenInParkour && LocalTimer.IsRunning))
                TimerWindow.IsOpen = false;
        }

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
            Parallel.Invoke(() =>
            {
                route.CheckCollision(player);
            });
        }
    }

    private IPlayerCharacter? localPlayer = null;

    private void OnFrameworkTick(IFramework framework)
    {
        if (!ClientState.IsLoggedIn || ClientState.IsPvP) return;
        localPlayer = ClientState.LocalPlayer;

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
                    //ICharacter player = ClientState.LocalPlayer;
                    if (localPlayer != null)
                    {
                        uint id = localPlayer.EntityId;

                        if (!trackedPlayers.ContainsKey(id))
                        {
                            trackedPlayers.Add(id, new Player(id, localPlayer, this));
                        }
                        else
                        {
                            trackedPlayers[id].actor = localPlayer;

                            bool lastGrounded = trackedPlayers[id].isGrounded;
                            trackedPlayers[id].UpdateState();

                            if (localPlayer.Position != trackedPlayers[id].position || lastGrounded != trackedPlayers[id].isGrounded)
                            {
                                trackedPlayers[id].Moved(localPlayer.Position);
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
            territoryHelper.GetLocationID();
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e.ToString());
        }
    }

    private void OnLogout(int type, int code)
    {
        LocalTimer.Reset();
        LoadedRoutes.Clear();
    }

    // Triggered whenever TerritoryHelper learns the ID of the location we're at
    public void AddressChanged(Address address)
    {
        Log.Debug("Detected area change: " + address.ReadableName);

        CurrentAddress = address;

        LocalTimer.Reset();
        LoadedRoutes.Clear();

        try
        {
            Storage.UpdateRouteCache();
            LoadedRoutes = Storage.RouteCache.Values.Where(r => r.Address.LocationId == address.LocationId).ToList();

            foreach (Route route in LoadedRoutes) 
            {
                if (route.Records == null)
                {
                    route.Records = new();
                }
            }

            DisplayedRecord = null;

            // Kick everyone from parkour when you change zones
            foreach (var player in trackedPlayers)
            {
                player.Value.inParkour = false;
                player.Value.raceLine.Clear();
            }

            if (LoadedRoutes.Count() > 0 && Configuration.AnnounceLoadedRoutes)
            {
                ChatGui.Print($"[RACE] Loaded {LoadedRoutes.Count()} route(s) in this area.");
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

    // Triggered when a player starts any loaded route
    private void OnStart(object? sender, Player e)
    {
        if (e.id == ClientState.LocalPlayer.EntityId)
        {
            LocalTimer.Restart();

            // Display Timer
            if (Configuration.ShowWhenInParkour && !TimerWindow.IsOpen)
            {
                TimerWindow.IsOpen = true;
            }
        }

        Route? route = sender as Route;

        if (Configuration.LogStart)
            PayloadedChat((IPlayerCharacter)e.actor, $" just started {route.Name}");
    }

    // Triggered whenever a player finished any loaded route
    private void OnFinish(object? sender, (Player, Record) e)
    {
        DataQueue.QueueDataOperation(async () =>
        {
            if (localPlayer != null && e.Item1.actor.EntityId == localPlayer.EntityId)
            {
                LocalTimer.Stop();
                HideTimer();
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

            route.Records.Add(e.Item2 as Record);
            route.Records = route.Records.OrderBy(r => r.Time.TotalNanoseconds).ToList();

            if (localPlayer != null && e.Item1.actor.EntityId == localPlayer.EntityId)
            {
                route.ClientFinishes++;
            }


            Storage.RouteCache[route.Id.ToString()] = route;
            await Storage.AddRoute(route); // Update the entry for this route
        });
    }


    // Triggered when a player fails any loaded route
    private void OnFailed(object? sender, Player e)
    {
        if (localPlayer != null && e.actor.EntityId == localPlayer.EntityId)
        {
            Route? route = sender as Route;
            if (route != null)
            {
                route.ClientFails++;

                DataQueue.QueueDataOperation(async () =>
                {
                    Storage.RouteCache[route.Id.ToString()].ClientFails = route.ClientFails;
                    await Storage.AddRoute(route);
                });
            }

            LocalTimer.Reset();
            HideTimer();
        }

        if (Configuration.LogFails)
        {
            PayloadedChat((IPlayerCharacter)e.actor, " just failed the parkour.");
        }
    }

    private void HideTimer()
    {
        if (!Configuration.ShowWhenInParkour) return;
        if (!TimerWindow.IsOpen) return;

        // Disable after set time
        Task.Delay(Configuration.SecondsShownAfter * 1000).ContinueWith(_ =>
        {
            if (LocalTimer.IsRunning) return; // Dont disable if we're back in parkour.
            TimerWindow.IsOpen = false;
        });
    }

    public void AddRoute(Route route)
    {
        bool containsRoute = Storage.RouteCache.ContainsKey(route.Id.ToString());

        DataQueue.QueueDataOperation(async () =>
        {
            await Storage.AddRoute(route);
            if (!containsRoute)
            {
                Storage.RouteCache.Add(route.Id.ToString(), route);
            }

            // Just reload all routes for the area when we import a new one
            List<Route> addressRoutes = Storage.RouteCache.Values.Where(r => r.Address.LocationId == CurrentAddress.LocationId).ToList();
            LoadedRoutes = addressRoutes;
            DisplayedRecord = null;
        });

        SubscribeToRouteEvents();
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

        LocalTimer.Stop();
        FontManager.Dispose();

        MainWindow.Dispose();
        TriggerOverlay.Dispose();

        Storage.Dispose();
        Configuration.Save();

        DataQueue.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui

        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleConfigUI() => MainWindow.Toggle();
    public void ToggleTriggerUI() => TriggerOverlay.Toggle();
}
