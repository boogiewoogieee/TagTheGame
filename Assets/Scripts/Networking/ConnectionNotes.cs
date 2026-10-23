using System.Collections.Generic;
using UnityEngine;

// Small facts this machine needs across scene changes, kept on the NetworkManager object
// because that is the one object that survives them: the player's own name for the
// lobby, the names the host hands to the arena, and a message for the menu after a
// disconnect. Nothing here travels over the network by itself.
public class ConnectionNotes : MonoBehaviour
{
    // Typed on the Joining screen; the lobby registers this machine with it.
    public string LocalPlayerName { get; set; } = "";

    // Shown on the Joining screen the next time the menu opens, then cleared.
    public string LastDisconnectMessage { get; set; } = "";

    // Host only: lobby names by client id, copied into the match stats when the arena starts.
    public Dictionary<ulong, string> PlayerNames { get; } = new Dictionary<ulong, string>();
}
