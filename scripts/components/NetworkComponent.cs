using Nez;

namespace nez.net.components;

public class NetworkComponent : Component
{
    private NetworkManager _networkManager;
    
    public NetworkComponent(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }
    
    // TODO: Add more methods here
}