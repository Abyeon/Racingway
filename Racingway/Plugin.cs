using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision.Triggers;
using Racingway.Utils;
using Racingway.Utils.Storage;
using Racingway.Windows;
using ZLinq;

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

    internal LocalDatabase? Storage { get; init; }
    public DataQueue DataQueue { get; init; }

    internal TerritoryHelper territoryHelper { get; set; }

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Racingway");
    public FontManager FontManager { get; init; }

    public TriggerOverlay TriggerOverlay { get; init; }
    public MainWindow MainWindow { get; init; }
    public TimerWindow TimerWindow { get; init; }

    public List<Route> LoadedRoutes { get; set; } = new();

    public Record? DisplayedRecord { get; set; }
    public Record? ClientBestRecord { get; set; }
    public ObjectId? SelectedRoute { get; set; }
    public ITrigger? SelectedTrigger { get; set; }
    public ITrigger? HoveredTrigger { get; set; }
    public Stopwatch LocalTimer { get; set; }

    public Address? CurrentAddress { get; set; }

    private IPlayerCharacter? localPlayer = null;
    private DateTime lastAutoCleanupTime = DateTime.MinValue;
    private readonly TimeSpan autoCleanupInterval = TimeSpan.FromHours(1); // Run cleanup once per hour

    public Dictionary<uint, Player> trackedPlayers = new();

    public Dictionary<Type, bool> TriggerToggles { get; set; } = new Dictionary<Type, bool>
    {
        {typeof(Start), true },
        {typeof(Loop), true },
        {typeof(Checkpoint), true },
        {typeof(Fail), true },
        {typeof(Finish), true }
    };

    // Add throttling for collision checks - we don't need to check every frame
    //private DateTime lastCollisionCheck = DateTime.MinValue;
    //private readonly TimeSpan collisionCheckInterval = TimeSpan.FromMilliseconds(16); // ~60 checks per second

    public Plugin()
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

                Plugin.ChatGui.PrintError(
                    $"[RACE] Due to changes in the database, Racingway has wiped your previous data.. Apologies for this!"
                );

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

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand) {
                HelpMessage = "Open the main UI\n" +
                "/race quit → Quit current race"
            }
        );


        if (Storage != null)
        {
            Framework.Update += OnFrameworkTick;
            ClientState.TerritoryChanged += OnTerritoryChange;
            ClientState.Logout += OnLogout;

            PluginInterface.UiBuilder.Draw += DrawUI;

            // This adds a button to the plugin installer entry of this plugin which allows
            // to toggle the display status of the main ui
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

            // Enable overlay if config calls for it
            ShowHideOverlay();

            // Update our address when plugin first loads
            territoryHelper.GetLocationID();

            // Disabling till I can get this stable
            // Try to fetch routes from RouteLists
            //try
            //{
            //    foreach (string url in Configuration.RouteList)
            //    {
            //        Log.Debug(url);
            //        ShareHelper.ImportRoutesFromURL(url, this);
            //    }
            //} catch (Exception ex)
            //{
            //    Log.Error(ex.ToString());
            //}
        } else
        {
            string err = "Racingway was unable to initialize DB. The plugin will be unable to function normally. This may be because you're running two instances of the plugin.";
            Plugin.ChatGui.PrintError("[RACE] " + err);
            Plugin.Log.Error(err);
        }
    }

    public List<(Func<bool>, Stopwatch)> polls = new();

    public void ShowHideOverlay()
    {
        if (Configuration.DrawTimer)
        {
            TimerWindow.IsOpen = true;
        }
        else if (!(Configuration.ShowWhenInParkour && LocalTimer.IsRunning))
        {
            TimerWindow.IsOpen = false;
        }

        if (Configuration.DrawRacingLines || Configuration.DrawTriggers || DisplayedRecord != null)
        {
            TriggerOverlay.IsOpen = true;
        }
        else
        {
            TriggerOverlay.IsOpen = false;
        }
    }

    public void CheckCollision(Player player)
    {
        if (LoadedRoutes == null || LoadedRoutes.Count == 0)
            return;

        // Disabling this for now
        // TODO: Add toggle for inaccurate but performant collision checking

        // Skip collision check if we just did one very recently
        //if (DateTime.UtcNow - _lastCollisionCheck < _collisionCheckInterval)
        //    return;

        //_lastCollisionCheck = DateTime.UtcNow;

        // Ensure player state is updated
        player.UpdateState();

        foreach (var route in LoadedRoutes)
        {
            // Run collision checks off-thread
            Task.Run(() =>
            {
                route.CheckCollision(player);
            });
        }
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!ClientState.IsLoggedIn || ClientState.IsPvP || ClientState.LocalPlayer == null || Storage == null)
            return;

        localPlayer = ClientState.LocalPlayer;

        // Check if it's time to run auto-cleanup
        if (DateTime.Now - lastAutoCleanupTime > autoCleanupInterval)
        {
            // Run cleanup in the background to avoid impacting FPS
            Task.Run(async () =>
            {
                try
                {
                    await Storage.RunRoutesAutoCleanup();
                    lastAutoCleanupTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Log.Error($"Error during auto-cleanup: {ex}");
                }
            });
        }

        if (polls != null && polls.Count > 0)
        {
            List<(Func<bool>, Stopwatch)> toRemove = new();

            // Loop through requested polling tasks
            foreach (var poll in polls)
            {
                try
                {
                    bool result = poll.Item1.Invoke();

                    if (result == true || poll.Item2.ElapsedMilliseconds > 1000)
                        toRemove.Add(poll);
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
            }
            catch (Exception e)
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
        if (Storage == null) return;

        Log.Debug("Detected area change: " + address.ReadableName);

        CurrentAddress = address;

        HideTimer();
        LocalTimer.Reset();
        LoadedRoutes.Clear();

        try
        {
            Storage.UpdateRouteCache();
            LoadedRoutes = Storage
                .RouteCache.Values.Where(r => r.Address.LocationId == address.LocationId)
                .ToList();

            // Reset map markers before placing new ones
            TerritoryHelper.ResetMapMarkers();

            foreach (Route route in LoadedRoutes)
            {
                route.AddMapMarkers();
                route.AddMinimapMarkers();

                if (route.Records == null)
                {
                    route.Records = new();
                }
            }

            DisplayedRecord = null;

            // Kick everyone from parkour when you change zones
            foreach (var player in trackedPlayers)
            {
                KickFromParkour(player.Value);
            }

            if (LoadedRoutes.Count() > 0 && Configuration.AnnounceLoadedRoutes)
            {
                ChatGui.Print($"[RACE] Loaded {LoadedRoutes.Count()} route(s) in this area.");
            }

            if (LoadedRoutes.Count > 0)
                SelectedRoute = LoadedRoutes.First().Id;
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }

        SubscribeToRouteEvents();
    }

    public void KickFromParkour(Player player)
    {
        player.inParkour = false;
        player.timer.Reset();
        player.currentSplits.Clear();
        player.lapsFinished = 0;
        player.ClearLine();

        foreach (var route in LoadedRoutes)
        {
            route.PlayersInParkour.RemoveAll(x => x.Item1 == player);
        }
    }

    public void KickClientFromParkour()
    {
        foreach (Player player in trackedPlayers.Values)
        {
            if (player.isClient && player.inParkour)
            {
                KickFromParkour(player);
                HideTimer();
                LocalTimer.Reset();
            }
        }
    }

    public void SubscribeToRouteEvents()
    {
        foreach (var route in LoadedRoutes)
        {
            route.OnStarted -= OnStart;
            route.OnCheckpoint -= OnCheckpoint;
            route.OnFinished -= OnFinish;
            route.OnFailed -= OnFailed;
            route.OnStarted += OnStart;
            route.OnCheckpoint += OnCheckpoint;
            route.OnFinished += OnFinish;
            route.OnFailed += OnFailed;
        }
    }

    public void UnsubscribeFromRouteEvents()
    {
        foreach (var route in LoadedRoutes)
        {
            route.OnStarted -= OnStart;
            route.OnCheckpoint -= OnCheckpoint;
            route.OnFinished -= OnFinish;
            route.OnFailed -= OnFailed;
        }
    }

    // Triggered when a player starts any loaded route
    private void OnStart(object? sender, Player e)
    {
        Route? route = sender as Route;
        if (route == null) return;

        if (e.isClient)
        {
            // Display Timer
            LocalTimer.Restart();
            if (Configuration.ShowWhenInParkour && !TimerWindow.IsOpen)
            {
                TimerWindow.IsOpen = true;
            }

            // Get best time
            List<Record> records = route.Records;
            records.AsValueEnumerable().Where(x => x.IsClient).OrderBy(x => x.Time).ToArray();

            if (records.Count > 0)
            {
                ClientBestRecord = records.First();
            }
        }

        if (Configuration.LogStart)
            PayloadedChat(e, $" just started {route.Name}");
    }

    private void QueueSplit(Player player, long difference)
    {
        if (player.isClient)
        {
            string pretty = Time.PrettyFormatTimeSpan(TimeSpan.FromMilliseconds(difference));

            Task.Run(async () =>
            {
                bool positive = difference > 0;
                TimerWindow.splits.Add(difference);
                await Task.Delay(3000);
                TimerWindow.splits.Remove(difference);
            });

            //Plugin.ChatGui.Print($"Hit Checkpoint {player.currentSplits.Count} in {pretty}");
        }
    }

    private void OnCheckpoint(object? sender, Player e)
    {
        Route? route = sender as Route;
        if (route == null) return;
        if (ClientBestRecord == null) return;
        if (ClientBestRecord.Splits == null) return;
        if (ClientBestRecord.Splits.Length == 0) return;

        long currSplit = e.currentSplits.Last().offset;
        long specifiedSplit = ClientBestRecord.Splits[e.currentSplits.Count - 1];
        long difference = currSplit - specifiedSplit;

        QueueSplit(e, difference);
    }

    // Triggered whenever a player finished any loaded route
    private void OnFinish(object? sender, (Player, Record) e)
    {
        if (Storage == null) return;

        Route? route = sender as Route;
        if (route == null)
        {
            Plugin.ChatGui.PrintError("[RACE] Route is null.");
            return;
        }

        // Immediately handle UI updates and local player actions
        if (localPlayer != null && e.Item1.actor.EntityId == localPlayer.EntityId)
        {
            if (ClientBestRecord != null && ClientBestRecord.Splits != null && ClientBestRecord.Splits.Length != 0)
            {
                long currSplit = (long)e.Item2.Time.TotalMilliseconds;
                long specifiedSplit = (long)ClientBestRecord.Time.TotalMilliseconds;
                long difference = currSplit - specifiedSplit;
                QueueSplit(e.Item1, difference);
            }
            LocalTimer.Stop();
            HideTimer();
            route.ClientFinishes++;
        }

        // Show finish message if enabled, without waiting for database operations
        if (Configuration.LogFinish)
        {
            var prettyPrint = Time.PrettyFormatTimeSpan(e.Item2.Time);
            PayloadedChat(e.Item1, $" just finished {route.Name} in {prettyPrint} and {e.Item2.Distance} units.");
        }

        Record record = e.Item2;

        // Store reference to route ID to avoid potential race conditions
        var routeId = route.Id.ToString();

        // Use debounced write for database operations to minimize FPS impact
        DataQueue.QueueDataOperation(async () =>
        {
            try
            {
                // Perform all in-memory and database operations on background thread
                if (Storage.RouteCache.TryGetValue(routeId, out Route? cachedRoute))
                {
                    // Update in-memory route data
                    cachedRoute.Records.Add(record);
                    cachedRoute.Records = cachedRoute
                        .Records.OrderBy(r => r.Time.TotalNanoseconds)
                        .ToList();

                    // Persist to database (using debounced write)
                    await Storage.AddRoute(cachedRoute);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error saving record: {ex.Message}");
            }
        });
    }

    // Triggered when a player fails any loaded route
    private void OnFailed(object? sender, Player e)
    {
        if (Storage == null) return;

        Route? route = sender as Route;
        if (route == null)
            return;

        if (localPlayer != null && e.actor.EntityId == localPlayer.EntityId)
        {
            LocalTimer.Reset();
            HideTimer();

            route.ClientFails++;

            // Store reference to route ID to avoid potential race conditions
            var routeId = route.Id.ToString();
            var failCount = route.ClientFails;

            DataQueue.QueueDataOperation(async () =>
            {
                try
                {
                    // Perform all database operations on background thread
                    if (Storage.RouteCache.TryGetValue(routeId, out Route? cachedRoute))
                    {
                        cachedRoute.ClientFails = failCount;
                        await Storage.AddRoute(cachedRoute);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error updating fails count: {ex.Message}");
                }
            });
        }

        if (Configuration.LogFails)
        {
            PayloadedChat(e, " just failed the parkour.");
        }
    }

    private void HideTimer()
    {
        if (Configuration.DrawTimer)
            return;
        if (!Configuration.ShowWhenInParkour)
            return;
        if (!TimerWindow.IsOpen)
            return;

        // Disable after set time
        Task.Delay(Configuration.SecondsShownAfter * 1000)
            .ContinueWith(_ =>
            {
                if (LocalTimer.IsRunning)
                    return; // Dont disable if we're back in parkour.
                TimerWindow.IsOpen = false;
            });
    }

    public void AddRoute(Route route)
    {
        if (CurrentAddress == null || Storage == null) return;

        bool containsRoute = Storage.RouteCache.ContainsKey(route.Id.ToString());

        DataQueue.QueueDataOperation(async () =>
        {
            await Storage.AddRoute(route);
            if (!containsRoute)
            {
                Storage.RouteCache.Add(route.Id.ToString(), route);
            }

            // Pretend we just loaded into the zone.
            AddressChanged(CurrentAddress);
            DisplayedRecord = null;
        });

        SubscribeToRouteEvents();
    }

    public void AddRoutes(List<Route> routes)
    {
        if (CurrentAddress == null || Storage == null) return;

        bool reloadZone = false;

        foreach (Route route in routes)
        {
            bool containsRoute = Storage.RouteCache.ContainsKey(route.Id.ToString());
            containsRoute |= Storage.RouteCache.Any(x => x.Value.Name == route.Name);

            DataQueue.QueueDataOperation(async () =>
            {
                await Storage.AddRoute(route);
                if (!containsRoute)
                {
                    if (route.Address == CurrentAddress) reloadZone = true;
                    Log.Debug($"Adding {route.Name} to routes.");
                    Storage.RouteCache.Add(route.Id.ToString(), route);
                }

                // TODO: Update routes if a player already has them. Preserve previous data, just update triggers/metadata
            });
        }

        DataQueue.QueueDataOperation(() =>
        {
            if (reloadZone)
            {
                // Pretend we just loaded into the zone.
                AddressChanged(CurrentAddress);
                DisplayedRecord = null;

                SubscribeToRouteEvents();
            }
        });
    }

    public static void PayloadedChat(Player player, string message)
    {
        PlayerPayload payload = new PlayerPayload(
            player.Name,
            player.HomeworldRow
        );
        TextPayload text = new TextPayload(message);
        SeString chat = new SeString(new Payload[] { payload, text });

        Plugin.ChatGui.Print(chat);
    }

    public ICharacter[] GetPlayers(IEnumerable<IGameObject> gameObjects)
    {
        IGameObject[] objects = gameObjects.AsValueEnumerable().Where(obj => obj is IPlayerCharacter).ToArray();
        ICharacter[] players = objects.Cast<ICharacter>().ToArray();
        return players;
    }

    public IPlayerCharacter GetPlayer(IEnumerable<IGameObject> gameObjects, uint actorId)
    {
        return (IPlayerCharacter)
            gameObjects.AsValueEnumerable().Where(obj => obj is IPlayerCharacter && obj.EntityId == actorId).First();
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

        if (Storage != null)
        {
            Storage.Dispose();
        }

        Configuration.Save();

        DataQueue.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        if (args.ToLower().Trim().Contains("quit"))
        {
            KickClientFromParkour();
        }
        else
        {
            ToggleMainUI();
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

    public void ToggleTriggerUI() => TriggerOverlay.Toggle();
}
