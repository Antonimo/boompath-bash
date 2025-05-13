// Struct to hold player data for UI and logic
public struct LobbyPlayerData
{
    public string PlayerId;    // Unique UGS Player ID
    public string DisplayName;
    public bool IsHost;
    public bool IsReady;
    public bool IsLocal;       // Is this player the local client?
}