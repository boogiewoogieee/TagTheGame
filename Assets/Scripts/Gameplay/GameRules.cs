using UnityEngine;

// Every tunable gameplay number lives here, so balancing the game means editing one
// asset instead of changing code.
[CreateAssetMenu(fileName = "GameRules", menuName = "TagTheGame/Game Rules")]
public class GameRules : ScriptableObject
{
    [Tooltip("Seconds from the start of a round until the bomb explodes.")]
    [Min(1f)]
    public float fuseSeconds = 30f;

    [Tooltip("How close the holder must get to the other player to pass the bomb, in metres.")]
    [Min(0.1f)]
    public float passRange = 1.5f;

    [Tooltip("Seconds a player must hold the bomb before passing it on. Stops instant pass-backs.")]
    [Min(0f)]
    public float passCooldown = 1f;

    [Tooltip("Extra distance the host accepts when it checks a pass, to make up for network delay, in metres.")]
    [Min(0f)]
    public float latencyTolerance = 1f;

    [Tooltip("Round wins needed to win the match. 1 makes a single round the whole match.")]
    [Min(1)]
    public int roundsToWin = 2;

    [Tooltip("Pause between rounds, in seconds: the round result and the score stay on screen while the players are sent back to their spawn points.")]
    [Min(1f)]
    public float betweenRoundsSeconds = 4f;

    [Tooltip("Scene offered as \"Next level\" on the results screen. Leave empty while there is only one arena.")]
    public string nextArenaScene = "";
}
