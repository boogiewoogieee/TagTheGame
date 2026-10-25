using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

// The list of open online games on the Joining screen. It asks SessionService for the
// games while the screen is showing, draws a row with a Join button for each, and hands
// the chosen game to MenuNetwork, which does the joining like it does for an address.
[RequireComponent(typeof(UIDocument))]
public class GameListPanel : MonoBehaviour
{
    [Tooltip("Does the joining; the list only says which game.")]
    public MenuNetwork network;

    [Tooltip("Seconds between automatic refreshes while the list is on screen.")]
    [Min(2f)]
    public float autoRefreshSeconds = 5f;

    private VisualElement _screen;
    private ScrollView _list;
    private Label _status;
    private Button _refresh;
    private SessionService _sessions;
    private bool _refreshing;
    private bool _wasVisible;
    private float _nextRefresh;

    private void OnEnable()
    {
        _screen = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("Joining");
        _list = _screen.Q<ScrollView>("GameList");
        _status = _screen.Q<Label>("GameListStatus");
        _refresh = _screen.Q<Button>("RefreshGames");
        _refresh.clicked += RefreshNow;

        NetworkManager networkManager = NetworkManager.Singleton;
        _sessions = networkManager != null ? networkManager.GetComponent<SessionService>() : null;
        if (_sessions == null)
        {
            _status.text = "Online games are not available. Join by address instead.";
        }
    }

    private void OnDisable()
    {
        if (_refresh != null)
        {
            _refresh.clicked -= RefreshNow;
        }
    }

    private void Update()
    {
        if (_sessions == null || network == null)
        {
            return;
        }

        // Look again as soon as the screen opens, then every few seconds while it stays open.
        bool visible = _screen.resolvedStyle.display == DisplayStyle.Flex;
        if (visible && !_wasVisible)
        {
            _nextRefresh = 0f;
        }

        _wasVisible = visible;

        // Play Mode shutdown and scene changes can remove the NetworkManager before this object.
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return;
        }

        bool busy = network.IsConnecting || networkManager.IsListening;
        _list.SetEnabled(!busy);
        _refresh.SetEnabled(!busy && !_refreshing);
        if (visible && !busy && !_refreshing && Time.unscaledTime >= _nextRefresh)
        {
            Refresh();
        }
    }

    private void RefreshNow()
    {
        _nextRefresh = 0f;
    }

    private async void Refresh()
    {
        _refreshing = true;
        if (_list.childCount == 0)
        {
            _status.text = "Looking for games...";
        }

        List<GameListing> games = await _sessions.ListGamesAsync();
        if (this == null)
        {
            return;
        }

        _refreshing = false;
        _nextRefresh = Time.unscaledTime + autoRefreshSeconds;
        Show(games, _sessions.LastError);
    }

    private void Show(List<GameListing> games, string error)
    {
        _list.Clear();
        foreach (GameListing game in games)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("game-row");

            Label name = new Label(GameListRules.Describe(game));
            name.AddToClassList("game-row-name");
            row.Add(name);

            GameListing chosen = game;
            Button join = new Button(() => network.JoinOnline(chosen)) { text = "Join" };
            join.AddToClassList("button");
            join.AddToClassList("game-row-join");
            row.Add(join);

            _list.Add(row);
        }

        if (!string.IsNullOrEmpty(error))
        {
            _status.text = error;
        }
        else
        {
            _status.text = games.Count == 0 ? "No open games yet. Host one, or join by address." : "";
        }
    }
}
