// RoundOver is the short pause between rounds; MatchOver shows the results screen.
public enum MatchPhase : byte
{
    WaitingForPlayers,
    Playing,
    RoundOver,
    MatchOver,

    // The 3-2-1-GO at the start of a round: the holder is known, nobody may move yet.
    Countdown
}
