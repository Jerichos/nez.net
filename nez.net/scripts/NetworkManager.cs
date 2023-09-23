using nez.net.components;
using nez.net.transport;
using nez.net.transport.socket;

namespace nez.net;

public class NetworkManager
{
    private static NetworkManager _instance;
    public static NetworkManager Instance
    {
        get
        {
            if(_instance == null)
                _instance = new NetworkManager();

            return _instance;
        }
    }
    
    private ITransport _transport;

    public NetworkManager(){}
    
    public NetworkManager(ITransport transport)
    {
        _transport = transport;
    }
}
