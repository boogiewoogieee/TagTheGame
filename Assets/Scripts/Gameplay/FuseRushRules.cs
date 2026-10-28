using System;
using Unity.Netcode;
using UnityEngine;

// The holder's Fuse Rush at one moment, as server times: until when it runs, and when the
// cooldown began and ends. The host writes it; every machine reads it against the clock.
// It names its holder, so a pass never shows the old holder's rush on the new one.
public struct FuseRushState : INetworkSerializable, IEquatable<FuseRushState>
{
    public ulong HolderId;
    public double ActiveUntil;
    public double CooldownFrom;
    public double ReadyAt;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref HolderId);
        serializer.SerializeValue(ref ActiveUntil);
        serializer.SerializeValue(ref CooldownFrom);
        serializer.SerializeValue(ref ReadyAt);
    }

    public bool Equals(FuseRushState other)
    {
        return HolderId == other.HolderId && ActiveUntil == other.ActiveUntil && CooldownFrom == other.CooldownFrom && ReadyAt == other.ReadyAt;
    }

    public override bool Equals(object obj)
    {
        return obj is FuseRushState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ReadyAt.GetHashCode();
    }
}

// The rules of the Fuse Rush skill as pure functions: no scene, network or time access.
// The holder's machine checks them before it starts the rush, the host checks them again
// before it accepts, and the HUD reads the same numbers for the cooldown ring.
public static class FuseRushRules
{
    // A new round: the holder may rush from GO.
    public static FuseRushState NewRound(ulong holderId, double roundStartsAt)
    {
        return new FuseRushState { HolderId = holderId, ActiveUntil = 0, CooldownFrom = roundStartsAt, ReadyAt = roundStartsAt };
    }

    // The bomb changed hands: the new holder waits a little, so it cannot rush straight back.
    public static FuseRushState AfterPass(ulong holderId, double now, float afterPassSeconds)
    {
        return new FuseRushState { HolderId = holderId, ActiveUntil = 0, CooldownFrom = now, ReadyAt = now + afterPassSeconds };
    }

    // The cooldown counts from the press, so it includes the rush itself.
    public static FuseRushState Started(ulong holderId, double now, float rushSeconds, float cooldownSeconds)
    {
        return new FuseRushState { HolderId = holderId, ActiveUntil = now + rushSeconds, CooldownFrom = now, ReadyAt = now + cooldownSeconds };
    }

    // A state written for an earlier holder does not count yet: the host has not caught up
    // with the pass. The tolerance lets the host accept a press its clock sees a moment early.
    public static bool CanStart(MatchPhase phase, ulong holderId, ulong requesterId, double now, FuseRushState state, double tolerance)
    {
        return phase == MatchPhase.Playing && requesterId == holderId && state.HolderId == requesterId
            && now >= state.ReadyAt - tolerance;
    }

    public static bool IsActive(FuseRushState state, ulong clientId, double now)
    {
        return state.HolderId == clientId && now < state.ActiveUntil;
    }

    // 1 right after the cooldown began, falling to 0 when the rush is ready again.
    public static float CooldownLeft(FuseRushState state, double now)
    {
        double span = state.ReadyAt - state.CooldownFrom;
        if (span <= 0 || now >= state.ReadyAt)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)((state.ReadyAt - now) / span));
    }

    // 1 when the rush starts, falling to 0 when it ends.
    public static float ActiveLeft(FuseRushState state, double now, float rushSeconds)
    {
        if (rushSeconds <= 0f || now >= state.ActiveUntil)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)((state.ActiveUntil - now) / rushSeconds));
    }
}
