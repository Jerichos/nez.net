using System;
using System.Collections.Concurrent;
using nez.net.components;

namespace nez.net.transport.socket
{
public interface ISocketHandler
{
    bool IsRunning { get; }
    
    void Stop();

    int MaxBufferSize { get; set; }

    Delegate<NetworkMessage> OnMessageReceived { get; set; }
    Delegate<TransportCode> OnTransportMessage { get; set; }
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
    void GetNetworkState(out ConcurrentDictionary<Guid, NetworkIdentity> networkEntities, out ConcurrentDictionary<Guid, NetworkComponent> networkComponents);
}

public interface ISocketClientHandler : ISocketHandler
{
    void Start(string ipAddress, int port);
    void Send(NetworkMessage message);
    public NetworkState NetworkState { get; set; }
}
}
