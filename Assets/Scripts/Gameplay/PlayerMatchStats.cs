using System;
using Unity.Collections;
using Unity.Netcode;

// One player's line on the results screen. Unmanaged so it can live in a NetworkList on
// the host and reach the client as plain bytes, like LobbyPlayer.
public struct PlayerMatchStats : INetworkSerializable, IEquatable<PlayerMatchStats>
{
    public ulong ClientId;
    public FixedString64Bytes Name;
    public int RoundsWon;
    public float SecondsHoldingBomb;
    public int Passes;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref RoundsWon);
        serializer.SerializeValue(ref SecondsHoldingBomb);
        serializer.SerializeValue(ref Passes);
    }

    public bool Equals(PlayerMatchStats other)
    {
        return ClientId == other.ClientId && Name.Equals(other.Name) && RoundsWon == other.RoundsWon
            && SecondsHoldingBomb == other.SecondsHoldingBomb && Passes == other.Passes;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerMatchStats other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ClientId.GetHashCode();
    }
}
