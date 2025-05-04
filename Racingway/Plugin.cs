using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
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
using Racingway.Utils;
using Racingway.Utils.Storage;
using Racingway.Windows;

namespace Racingway;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IGameNetwork GameNetwork { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService]
    internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    internal LocalDatabase Storage { get; init; } = null!;
    public DataQueue DataQueue { get; init; } = null!;
    internal TerritoryHelper territoryHelper { get; set; } = null!;

    private const string CommandName = "/race";

    public Configuration Configuration { get; init; } = null!;
    public readonly WindowSystem WindowSystem = new("Racingway");
    public FontManager FontManager { get; init; } = null!;

    public TriggerOverlay TriggerOverlay { get; init; } = null!;
    public MainWindow MainWindow { get; init; } = null!;
    public TimerWindow TimerWindow { get; init; } = null!;

    public List<Route> LoadedRoutes { get; set; } = new();

    public Record DisplayedRecord { get; set; } = null!;
    public ObjectId? SelectedRoute { get; set; }
    public Stopwatch LocalTimer { get; set; } = null!;

    public Address CurrentAddress { get; set; } = null!;

    public IGameObject[] trackedNPCs = null!;

    // Add a throttling mechanism to limit collision checks
    private readonly Dictionary<uint, DateTime> _lastCollisionCheck =
        new Dictionary<uint, DateTime>();
    private const int COLLISION_CHECK_INTERVAL_MS = 50; // Limit checks to once per 50ms per player

    public Plugin()
    {
        try
        {
            LocalTimer = new Stopwatch();
            territoryHelper = new TerritoryHelper(this);

            Configuration =
                PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
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
                new CommandInfo(OnCommand) { HelpMessage = "Open the main UI" }
            );

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
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
    }

    public Dictionary<uint, Player> trackedPlayers = new();

    public void ShowHideOverlay()
    {
        if (Configuration.DrawTimer)
        {
            TimerWindow.IsOpen = true;
        }
        else
        {
            if (!(Configuration.ShowWhenInParkour && LocalTimer.IsRunning))
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

    public List<(Func<bool>, Stopwatch)> polls = new();

    public void CheckCollision(Player player)
    {
        try
        {
            // Implement throttling to reduce excessive collision checks
            uint playerId = player.id;
            DateTime now = DateTime.Now;

            // Check if we've recently processed this player's collision
            if (_lastCollisionCheck.TryGetValue(playerId, out DateTime lastCheck))
            {
                // If last check was too recent, skip this one
                if ((now - lastCheck).TotalMilliseconds < COLLISION_CHECK_INTERVAL_MS)
                    return;
            }

            // Update the last check time for this player
            _lastCollisionCheck[playerId] = now;

            // Regular collision check logic
            if (LoadedRoutes == null || LoadedRoutes.Count == 0)
                return;

            // Check if this is the local player - use direct check for higher performance
            bool isLocalPlayer =
                player.actor != null
                && localPlayer != null
                && player.actor.EntityId == localPlayer.EntityId;

            // Process all collision checks directly for all players for better reliability
            // Only using Task.Run before caused inconsistent timing behavior
            foreach (Route route in LoadedRoutes)
            {
                try
                {
                    route.CheckCollision(player);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        $"Error checking collision for {(isLocalPlayer ? "local player" : "other player")}"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in CheckCollision method");
        }
    }

    private IPlayerCharacter? localPlayer = null;

    private void OnFrameworkTick(IFramework framework)
    {
        try
        {
            if (!ClientState.IsLoggedIn || ClientState.IsPvP)
                return;
            localPlayer = ClientState.LocalPlayer;

            // Simple timer logic - if timer is running, make sure window is visible
            if (LocalTimer.IsRunning)
            {
                // Show timer window when race is active
                TimerWindow.IsOpen = true;

                // Debug logging (every 10 seconds) for timer state
                if (Configuration.DebugMode && LocalTimer.ElapsedMilliseconds % 10000 < 50)
                {
                    Log.Debug(
                        $"Timer running: {LocalTimer.IsRunning}, Elapsed: {LocalTimer.ElapsedMilliseconds}ms"
                    );
                }

                // Debug code for parkour state (every 5 seconds)
                if (LocalTimer.ElapsedMilliseconds % 5000 < 50 && localPlayer != null)
                {
                    foreach (var route in LoadedRoutes)
                    {
                        if (
                            trackedPlayers.ContainsKey(localPlayer.EntityId)
                            && route.IsPlayerInParkour(trackedPlayers[localPlayer.EntityId])
                        )
                        {
                            route.DumpParkourState();
                            break;
                        }
                    }
                }
            }
            else if (!Configuration.DrawTimer)
            {
                // Only hide timer if configuration doesn't force it to be shown
                if (!Configuration.ShowWhenInParkour)
                {
                    TimerWindow.IsOpen = false;
                }
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

                                if (
                                    player.Position != trackedPlayers[id].position
                                    || lastGrounded != trackedPlayers[id].isGrounded
                                )
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

                                if (
                                    localPlayer.Position != trackedPlayers[id].position
                                    || lastGrounded != trackedPlayers[id].isGrounded
                                )
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
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnFrameworkTick");
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

    // Optimized handling of route loading to improve performance
    public void AddressChanged(Address address)
    {
        Log.Debug("Detected area change: " + address.ReadableName);

        CurrentAddress = address;

        HideTimer();
        LocalTimer.Reset();
        LoadedRoutes.Clear();

        try
        {
            // Optimize database access by using background task to load routes
            Task.Run(() =>
            {
                Storage.UpdateRouteCache();
                var newRoutes = Storage
                    .RouteCache.Values.Where(r => r.Address.LocationId == address.LocationId)
                    .ToList();

                // Pre-load records for each route to avoid database hitches later
                foreach (var route in newRoutes)
                {
                    // Cache the ordered records list
                    _ = route.GetRecordsOptimized();
                }

                // Update the loaded routes on the main thread
                Framework.RunOnFrameworkThread(() =>
                {
                    LoadedRoutes = newRoutes;
                    Log.Debug(
                        $"Loaded {LoadedRoutes.Count} routes for area {address.ReadableName}"
                    );

                    DisplayedRecord = null;

                    // Kick everyone from parkour when you change zones
                    foreach (var player in trackedPlayers)
                    {
                        player.Value.inParkour = false;
                        player.Value.raceLine.Clear();
                    }

                    if (LoadedRoutes.Count() > 0 && Configuration.AnnounceLoadedRoutes)
                    {
                        ChatGui.Print(
                            $"[RACE] Loaded {LoadedRoutes.Count()} route(s) in this area."
                        );
                    }

                    if (LoadedRoutes.Count > 0)
                        SelectedRoute = LoadedRoutes.First().Id;

                    // Set up event handlers right away
                    SubscribeToRouteEvents();
                });
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
    }

    public void SubscribeToRouteEvents()
    {
        // First unsubscribe to prevent duplicate subscriptions
        UnsubscribeFromRouteEvents();

        // Then subscribe to events for all routes
        foreach (var route in LoadedRoutes)
        {
            route.OnStarted += OnStart;
            route.OnFinished += OnFinish;
            route.OnFailed += OnFailed;

            // Log subscription for debugging
            Log.Debug($"Subscribed to events for route: {route.Name}");
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

    // Helper method to check if we're on the main thread
    private bool IsOnMainThread()
    {
        // We're in the main thread if we have a valid localPlayer reference
        // or we're currently executing in the framework Update callback
        try
        {
            var temp = ClientState.LocalPlayer;
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not on main thread"))
        {
            return false;
        }
    }

    // Triggered when a player starts any loaded route
    private void OnStart(object? sender, Player e)
    {
        try
        {
            // Handle local player timer
            if (ClientState.LocalPlayer != null && e.id == ClientState.LocalPlayer.EntityId)
            {
                // Explicitly reset and start the timer
                LocalTimer.Reset();
                LocalTimer.Start();

                // Log for debugging
                Log.Debug($"Started timer: {LocalTimer.ElapsedMilliseconds}ms");

                // Show timer window
                TimerWindow.IsOpen = true;
                ShowHideOverlay();
            }

            // Handle chat messages
            Route? route = sender as Route;
            if (route != null && Configuration.LogStart)
            {
                try
                {
                    Log.Debug($"Race start: {e.actor.Name}");
                    PayloadedChat((IPlayerCharacter)e.actor, $" just started {route.Name}");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in chat output on start");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnStart");
        }
    }

    // Triggered whenever a player finished any loaded route
    private void OnFinish(object? sender, (Player, Record) e)
    {
        try
        {
            var prettyPrint = Utils.Time.PrettyFormatTimeSpan(e.Item2.Time);
            Route? route = sender as Route;

            if (route == null)
            {
                ChatGui.PrintError("[RACE] Route is null.");
                return;
            }

            // Handle chat messages and timer for local player
            try
            {
                // Special case for local player
                if (localPlayer != null && e.Item1.actor.EntityId == localPlayer.EntityId)
                {
                    // Stop timer for local player
                    LocalTimer.Stop();
                    Log.Debug($"Stopped timer: {LocalTimer.ElapsedMilliseconds}ms");

                    // Hide timer after delay
                    HideTimer();

                    // Notify player of recorded time
                    ChatGui.Print(
                        $"[RACE] Your time of {prettyPrint} has been recorded for {route.Name}!"
                    );
                }

                // Chat message for all players
                Log.Debug($"Race finish: {e.Item1.actor.Name} in {prettyPrint}");
                PayloadedChat(
                    (IPlayerCharacter)e.Item1.actor,
                    $" just finished {route.Name} in {prettyPrint} and {e.Item2.Distance} units."
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in chat output on finish");
            }

            // Update records
            try
            {
                lock (route.Records)
                {
                    route.Records.Add(e.Item2 as Record);
                    route.InvalidateRecordCache();

                    if (localPlayer != null && e.Item1.actor.EntityId == localPlayer.EntityId)
                    {
                        route.ClientFinishes++;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating route records in memory");
            }

            // Queue database update
            DataQueue.QueueDataOperation(async () =>
            {
                try
                {
                    if (Storage.RouteCache.ContainsKey(route.Id.ToString()))
                    {
                        Storage.RouteCache[route.Id.ToString()] = route;
                        await Storage.AddRoute(route);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving race data to database");
                }
            });

            // Run cleanup if enabled
            if (Configuration.EnableAutoCleanup || route.EnableAutoCleanup)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000);
                        await DataQueue.QueueDataOperation(async () =>
                        {
                            try
                            {
                                await Storage.CleanupRecords(
                                    Configuration.MinTimeFilter,
                                    Configuration.MaxRecordsPerRoute,
                                    Configuration.RemoveNonClientRecords,
                                    Configuration.KeepPersonalBestOnly
                                );
                                Log.Information("Auto-cleanup completed");
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error during auto-cleanup");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error scheduling auto-cleanup");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in OnFinish");
        }
    }

    // Triggered when a player fails any loaded route
    private void OnFailed(object? sender, Player e)
    {
        try
        {
            // Ensure we're on the main thread for accessing ClientState.LocalPlayer
            if (!IsOnMainThread())
            {
                // If we're not on the main thread, queue this to run on the main thread
                Framework.RunOnFrameworkThread(() => OnFailed(sender, e));
                return;
            }

            Route? route = sender as Route;
            if (route == null)
                return;

            if (localPlayer != null && e.actor.EntityId == localPlayer.EntityId)
            {
                LocalTimer.Reset();
                HideTimer();

                // Update memory state immediately (non-blocking)
                route.ClientFails++;

                // Queue database update without blocking the main thread
                DataQueue.QueueDataOperation(async () =>
                {
                    try
                    {
                        Storage.RouteCache[route.Id.ToString()].ClientFails = route.ClientFails;
                        await Storage.AddRoute(route);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex, "Error saving failure data to database");
                    }
                });
            }

            if (Configuration.LogFails)
            {
                PayloadedChat((IPlayerCharacter)e.actor, " just failed the parkour.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnFailed");
        }
    }

    private void HideTimer()
    {
        // Don't hide if timer window should stay visible
        if (Configuration.DrawTimer)
            return;

        // Don't hide if we should show when in parkour
        if (!Configuration.ShowWhenInParkour)
            return;

        // Use configured delay
        Task.Delay(Configuration.SecondsShownAfter * 1000)
            .ContinueWith(_ =>
            {
                // Run on framework thread
                Framework.RunOnFrameworkThread(() =>
                {
                    // Don't hide if timer is running again
                    if (!LocalTimer.IsRunning)
                        TimerWindow.IsOpen = false;
                });
            });
    }

    public void AddRoute(Route route)
    {
        bool containsRoute = Storage.RouteCache.ContainsKey(route.Id.ToString());

        // Update the cache immediately for UI responsiveness
        if (!containsRoute)
        {
            Storage.RouteCache.Add(route.Id.ToString(), route);
        }
        else
        {
            Storage.RouteCache[route.Id.ToString()] = route;
        }

        // Pretend we just loaded into the zone to refresh UI
        AddressChanged(CurrentAddress);
        DisplayedRecord = null;

        // Queue the database update in the background
        DataQueue.QueueDataOperation(async () =>
        {
            try
            {
                await Storage.AddRoute(route);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error adding route to database");
            }
        });

        SubscribeToRouteEvents();
    }

    public void PayloadedChat(IPlayerCharacter player, string message)
    {
        PlayerPayload payload = new PlayerPayload(
            player.Name.ToString(),
            player.HomeWorld.Value.RowId
        );
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
        return (IPlayerCharacter)
            gameObjects.Where(obj => obj is IPlayerCharacter && obj.EntityId == actorId).First();
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
