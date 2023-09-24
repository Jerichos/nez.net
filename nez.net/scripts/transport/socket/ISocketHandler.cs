namespace nez.net.transport.socket
{
public interface ISocketHandler
{
    bool IsRunning { get; }
    
    void Stop();

    event Delegate<NetworkMessage> OnReceive;
    event Delegate<TransportCode> OnTransportMessage;
}

public interface ISocketServerHandler : ISocketHandler
{
    int MaxConnections { get; set; }
    void Start(int port);

    public NetworkState NetworkState { get; set; }
    
    // send to all clients
    void Send(NetworkMessage message);
    // send to specific client
    void Send(uint clientId, NetworkMessage message);
    
    void OnClientConnected(uint clientId);
}

public interface ISocketClientHandler : ISocketHandler
{
    void Start(string ipAddress, int port);
    void Send(NetworkMessage message);
    public NetworkState NetworkState { get; set; }
}
}
