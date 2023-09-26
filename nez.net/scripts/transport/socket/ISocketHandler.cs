using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using nez.net.components;

namespace nez.net.transport.socket
{
public interface ISocketHandler
{
    bool IsRunning { get; }
    
    void Stop();

    int MaxBufferSize { get; set; }

    Delegate<Socket, NetworkMessage> OnMessageReceived { get; set; }
    Delegate<TransportCode> OnTransportMessage { get; set; }
    
    // performance
    public long TotalBitsSent { get; }
    public long TotalBitsReceived { get;}

    public double SendBitRate { get; }          // TODO: this is not updated
    public double ReceiveBitRate { get; }
    
    Socket Socket { get; set; }
    void Send(Socket connection, NetworkMessage message);
}

public interface ISocketServerHandler : ISocketHandler
{
    int MaxConnections { get; set; }
    void Start(int port);

    public NetworkState NetworkState { get; set; }
    
    // send to all clients
    void Send(NetworkMessage message);
    // send to specific client
    void Send(ushort clientId, NetworkMessage message);
    void OnClientConnected(ushort clientId);
    void GetNetworkState(out ConcurrentDictionary<Guid, NetworkIdentity> networkEntities, out ConcurrentDictionary<Guid, NetworkComponent> networkComponents);
}

public interface ISocketClientHandler : ISocketHandler
{
    void Start(string ipAddress, int port);
    void Send(NetworkMessage message);
    public NetworkState NetworkState { get; set; }
}
}
