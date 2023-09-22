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

    public void InitializeServer(int port)
    {
        _transport.StartServer(port);
    }

    public void InitializeClient(string ipAddress, int port)
    {
        _transport.ConnectClient(ipAddress, port);
    }

    public void Close(ConnectionType connectionType)
    {
        if (connectionType == ConnectionType.SERVER)
        {
            _transport.StopServer();
        }
        else
        {
            _transport.StopClient();
        }
    }
    
    // TODO: Add more methods here
}
