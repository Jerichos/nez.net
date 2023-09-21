using nez.net.transport;

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
        _transport.InitializeServer(port);
    }

    public void InitializeClient(string ipAddress, int port)
    {
        _transport.InitializeClient(ipAddress, port);
    }

    public void Send(NetMessage message, ConnectionType connectionType)
    {
        if (connectionType == ConnectionType.Server)
        {
            _transport.ServerSend(message);
        }
        else
        {
            _transport.ClientSend(message);
        }
    }

    public NetMessage Receive(ConnectionType connectionType)
    {
        if (connectionType == ConnectionType.Server)
        {
            return _transport.ServerReceive();
        }
        else
        {
            return _transport.ClientReceive();
        }
    }

    public void Close(ConnectionType connectionType)
    {
        if (connectionType == ConnectionType.Server)
        {
            _transport.CloseServer();
        }
        else
        {
            _transport.CloseClient();
        }
    }
    
    // TODO: Add more methods here
}
