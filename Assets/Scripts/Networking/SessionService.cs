using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif

// The only class that talks to Unity's online services: sign-in, the list of open games,
// and creating, joining, leaving and locking a game. The menu calls it and shows LastError.
// It sits on the NetworkManager prefab, which survives scene changes, so the joined game
// stays known in the arena and after Return to lobby. Direct connect never comes here, so
// the game also starts and plays without internet.
[RequireComponent(typeof(NetworkManager))]
public class SessionService : MonoBehaviour
{
    public const string OfflineMessage = "Can't reach the online service. Use Direct connect.";
    public const string GameGoneMessage = "That game is no longer open. Refresh the list.";
    public const string JoinFailedMessage = "Couldn't join that game. It may have started, filled up or closed. Refresh the list.";
    public const string HostOfflineMessage = "Can't reach the online service. Tick \"LAN only (direct)\" to host without it.";

    [Tooltip("Players per game, host included.")]
    public int maxPlayers = 2;

    [Tooltip("How many open games one refresh asks the service for.")]
    public int listSize = 50;

    [Tooltip("Seconds before a request to the online service counts as failed. Some antivirus programs make requests hang instead of fail.")]
    public float timeoutSeconds = 15f;

    [Tooltip("A refresh sooner than this after the last one reuses the last list. The service refuses list requests that come too quickly.")]
    public float minRefreshSeconds = 2f;

    private NetworkManager _networkManager;
    private ISession _session;
    private List<GameListing> _lastList = new List<GameListing>();
    private float _lastListTime = float.NegativeInfinity;
    private bool _failing;

    public bool IsBusy { get; private set; }
    public string LastError { get; private set; } = "";
    public bool InOnlineGame => _session != null;
    public string CurrentGameName => _session != null ? _session.Name : "";

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        _networkManager.OnClientStopped += OnNetworkStopped;
    }

    private void OnDisable()
    {
        _networkManager.OnClientStopped -= OnNetworkStopped;
    }

    private void OnApplicationQuit()
    {
        // Leaving tells the service right away; otherwise a host's game stays listed until it times out.
        if (_session != null)
        {
            _ = LeaveGameAsync();
        }
    }

    // Open games other players can join, already filtered and sorted for the menu.
    public async Task<List<GameListing>> ListGamesAsync()
    {
        if (Time.realtimeSinceStartup - _lastListTime < minRefreshSeconds)
        {
            return new List<GameListing>(_lastList);
        }

        List<GameListing> games = new List<GameListing>();
        bool ok = await Run(async () =>
        {
            await EnsureSignedInAsync();
            QuerySessionsOptions query = new QuerySessionsOptions { Count = listSize };
            query.FilterOptions.Add(new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater));
            QuerySessionsResults results = await WithTimeout(MultiplayerService.Instance.QuerySessionsAsync(query));
            foreach (ISessionInfo info in results.Sessions)
            {
                games.Add(new GameListing
                {
                    Id = info.Id,
                    Name = info.Name,
                    PlayerCount = info.MaxPlayers - info.AvailableSlots,
                    MaxPlayers = info.MaxPlayers,
                    IsLocked = info.IsLocked,
                });
            }
        });

        // Counted from the answer, not the request: the first one also signs in and can take seconds.
        _lastListTime = Time.realtimeSinceStartup;
        _lastList = ok ? GameListRules.Visible(games, _session != null ? _session.Id : null) : new List<GameListing>();
        return new List<GameListing>(_lastList);
    }

    // Creates a listed game and starts this machine as its host. The menu's usual
    // "hosted" event then opens the lobby, exactly as with Direct connect.
    public Task<bool> CreateGameAsync(string typedName, string playerName)
    {
        return Run(async () =>
        {
            await EnsureSignedInAsync();
            SessionOptions options = new SessionOptions
            {
                Name = GameListRules.CleanGameName(typedName, playerName),
                MaxPlayers = maxPlayers,
            }.WithRelayNetwork();
            _session = await WithTimeout(MultiplayerService.Instance.CreateSessionAsync(options), LeaveIfLate);
        }, HostOfflineMessage);
    }

    // Joins a game from the list and starts this machine as its client.
    public Task<bool> JoinGameAsync(string gameId)
    {
        return Run(async () =>
        {
            await EnsureSignedInAsync();
            _session = await WithTimeout(MultiplayerService.Instance.JoinSessionByIdAsync(gameId), LeaveIfLate);
        }, JoinFailedMessage);
    }

    // Host only. A started match takes the game off the list; the lobby puts it back and
    // frees the seat of anyone who dropped out without leaving. Netcode's own gate still
    // refuses late joiners, so a failed request here only leaves the list out of date.
    public Task<bool> SetJoinableAsync(bool joinable)
    {
        IHostSession host = _session as IHostSession;
        bool dropOthers = joinable && HostIsAlone(host);
        if (host == null || (host.IsLocked == !joinable && !dropOthers))
        {
            return Task.FromResult(true);
        }

        return Run(async () =>
        {
            if (dropOthers)
            {
                await RemoveOtherPlayersAsync(host);
            }

            if (host.IsLocked == joinable)
            {
                host.IsLocked = !joinable;
                await WithTimeout(host.SavePropertiesAsync());
            }
        });
    }

    // Safe to call at any time; also stops the network if it is still running.
    public async Task LeaveGameAsync()
    {
        ISession session = _session;
        _session = null;
        if (session == null)
        {
            return;
        }

        try
        {
            await WithTimeout(session.LeaveAsync());
        }
        catch (SessionException e) when (e.Error == SessionError.SessionNotFound || e.Error == SessionError.SessionDeleted)
        {
            // The host left first and the game went with it: nothing left to leave.
        }
        catch (Exception e)
        {
            // Not worth an error for the player: the service drops the game on its own soon.
            Debug.LogWarning("Leaving the online game failed: " + e.Message);
        }
    }

    // Leave, Leave to menu and a lost connection all stop the network first, and the
    // package does not leave the online game by itself when that happens.
    private void OnNetworkStopped(bool wasHost)
    {
        if (_session != null)
        {
            _ = LeaveGameAsync();
        }
    }

    private async Task EnsureSignedInAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitializationOptions options = new InitializationOptions();
            string profile = AuthProfileRules.ProfileFor(Environment.GetCommandLineArgs(), PlayModeTag());
            if (profile != null)
            {
                options.SetProfile(profile);
            }

            await WithTimeout(UnityServices.InitializeAsync(options));
        }
        else if (UnityServices.State == ServicesInitializationState.Initializing)
        {
            // A request before this one is still starting the services; wait for it instead of starting twice.
            await WithTimeout(UnityServices.InitializeAsync());
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync());
        }
    }

    // A player whose game crashed or lost its connection never left, and the service would
    // keep the seat taken for a while. Netcode and the service number players differently,
    // so only the clear case is handled: a host alone on the network has nobody else in its game.
    private bool HostIsAlone(IHostSession host)
    {
        return host != null && _networkManager.IsServer
            && _networkManager.ConnectedClientsIds.Count <= 1 && host.Players.Count > 1;
    }

    private async Task RemoveOtherPlayersAsync(IHostSession host)
    {
        List<string> others = new List<string>();
        foreach (IReadOnlyPlayer player in host.Players)
        {
            if (player.Id != host.CurrentPlayer.Id)
            {
                others.Add(player.Id);
            }
        }

        foreach (string id in others)
        {
            try
            {
                await WithTimeout(host.RemovePlayerAsync(id));
            }
            catch (Exception)
            {
                // Usually the player's own Leave got there first; if not, the service frees the seat itself later.
            }
        }
    }

    // Two copies on one PC would sign in as the same player and could not join each other,
    // so a Multiplayer Play Mode virtual player signs in under its own profile.
    private static string PlayModeTag()
    {
#if UNITY_EDITOR
        IReadOnlyList<string> tags = CurrentPlayer.Tags;
        if (tags != null && tags.Count > 0)
        {
            return tags[0];
        }

        return CurrentPlayer.IsMainEditor ? null : "VirtualPlayer";
#else
        return null;
#endif
    }

    // One request at a time, with the failure turned into a sentence for the menu.
    // failMessage is what the player reads when the service gives no clearer reason.
    private async Task<bool> Run(Func<Task> request, string failMessage = OfflineMessage)
    {
        if (IsBusy)
        {
            LastError = "Still waiting for the online service.";
            return false;
        }

        IsBusy = true;
        LastError = "";
        try
        {
            await request();
            _failing = false;
            return true;
        }
        catch (TimeoutException)
        {
            LastError = failMessage;
            Warn("Online service: no answer within " + timeoutSeconds + " s.");
        }
        catch (SessionException e)
        {
            LastError = MessageFor(e.Error, failMessage);
            Warn("Online service: " + e.Error + " " + e.Message);
        }
        catch (RequestFailedException e)
        {
            LastError = failMessage;
            Warn("Online service request failed (" + e.ErrorCode + "): " + e.Message);
        }
        catch (Exception e)
        {
            LastError = failMessage;
            Warn("Online service: " + e.Message);
        }
        finally
        {
            IsBusy = false;
        }

        return false;
    }

    // Once per run of failures: the game list keeps refreshing while the service is
    // unreachable, and the same warning every few seconds would bury everything else.
    private void Warn(string message)
    {
        if (!_failing)
        {
            Debug.LogWarning(message);
        }

        _failing = true;
    }

    private static string MessageFor(SessionError error, string failMessage)
    {
        switch (error)
        {
            case SessionError.SessionNotFound:
            case SessionError.SessionDeleted:
            case SessionError.Forbidden:
                return GameGoneMessage;
            case SessionError.RateLimitExceeded:
                return "Too many requests. Wait a moment and try again.";
            default:
                return failMessage;
        }
    }

    private async Task WithTimeout(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) != task)
        {
            throw new TimeoutException();
        }

        await task;
    }

    // For create and join: if the answer comes after the player was told it failed,
    // leave that game again so it does not linger, half-started, in the list.
    private async Task<T> WithTimeout<T>(Task<T> task, Action<Task<T>> onLate = null) where T : class
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) != task)
        {
            if (onLate != null)
            {
                onLate(task);
            }

            throw new TimeoutException();
        }

        return await task;
    }

    private static async void LeaveIfLate<T>(Task<T> task) where T : class
    {
        try
        {
            ISession late = await task as ISession;
            if (late != null)
            {
                await late.LeaveAsync();
            }
        }
        catch (Exception)
        {
            // It failed after all, so there is nothing to leave.
        }
    }
}
