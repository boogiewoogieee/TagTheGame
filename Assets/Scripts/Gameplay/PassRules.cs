public enum PassResult
{
    Allowed,
    NotPlaying,
    SenderIsNotHolder,
    InvalidTarget,
    OnCooldown,
    TooFar
}

// The rules for passing the bomb, as a pure function: no scene, network or time access.
// The holder checks it before asking, the host checks it again before accepting, and
// it can be verified by calling it directly.
public static class PassRules
{
    public static PassResult Check(
        MatchPhase phase,
        ulong holderId,
        ulong senderId,
        ulong targetId,
        double secondsHeld,
        float passCooldown,
        float distance,
        float allowedDistance)
    {
        if (phase != MatchPhase.Playing)
        {
            return PassResult.NotPlaying;
        }

        if (senderId != holderId)
        {
            return PassResult.SenderIsNotHolder;
        }

        if (targetId == senderId)
        {
            return PassResult.InvalidTarget;
        }

        if (secondsHeld < passCooldown)
        {
            return PassResult.OnCooldown;
        }

        if (distance > allowedDistance)
        {
            return PassResult.TooFar;
        }

        return PassResult.Allowed;
    }
}
