// One row in the list of open games; SessionService fills it from the online service.
public struct GameListing
{
    public string Id;
    public string Name;
    public int PlayerCount;
    public int MaxPlayers;
    public bool IsLocked;
}
