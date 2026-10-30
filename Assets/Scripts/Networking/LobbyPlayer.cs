using System;
using Unity.Collections;
using Unity.Netcode;

// One row of the lobby list. Kept unmanaged (fixed-size string, no references) so it can
// live in a NetworkList and travel to the client as plain bytes.
public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
{
    public ulong ClientId;
    public FixedString64Bytes Name;
    public bool IsReady;

    // Index into the CharacterCatalog, or CharacterRules.None.
    public int CharacterIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref CharacterIndex);
    }

    public bool Equals(LobbyPlayer other)
    {
        return ClientId == other.ClientId && Name.Equals(other.Name) && IsReady == other.IsReady && CharacterIndex == other.CharacterIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is LobbyPlayer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ClientId.GetHashCode();
    }
}
