using System;
using System.Net.Sockets;
using nez.net.components;
using ZeroFormatter;
using ZeroFormatter.Formatters;

namespace nez.net.transport.socket
{
public class SocketTransport
{
    public ISocketServerHandler Server { get; }
    public ISocketClientHandler Client { get; }

    public bool IsServerRunning => Server.IsRunning;
    public bool IsClientRunning => Client.IsRunning;

    public int MaxBufferSize { get; private set; } = 256; // 2kb, default was 1024
    
    private readonly NetworkState _networkState = new();
 
    public SocketTransport()
    {
        Server = new SocketServer(MaxBufferSize, _networkState);
        Client = new SocketClient(MaxBufferSize, _networkState);
        
        Formatter<DefaultResolver, NetworkIdentity>.Register(new NetworkIdentityFormatter<DefaultResolver>());
        Formatter<DefaultResolver, NetworkComponent>.Register(new NetworkComponentFormatter<DefaultResolver>());
        
        Server.OnMessageReceived += OnServerReceive;
        Client.OnMessageReceived += OnClientReceive;
    }

    private void OnServerReceive(Socket connection, NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.NETWORK_STATE:
                break;
            case MessageType.TRANSPORT:
                break;
            case MessageType.MIRROR:
                break;
            case MessageType.PING:
                Server.Send(connection, new PongMessage());
                break;
            case MessageType.PONG:
                break;
            case MessageType.URI:
                break;
            case MessageType.SYNC:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    private void OnClientReceive(Socket connection, NetworkMessage message)
    {
        if (message == null)
            return;
        
        switch (message.Type)
        {
            case MessageType.NETWORK_STATE:
                var networkStateMessage = message as NetworkStateMessage;
                _networkState.SetNetworkState(networkStateMessage.NetworkEntities, networkStateMessage.NetworkComponents);
                break;
            case MessageType.TRANSPORT:
                break;
            case MessageType.MIRROR:
                break;
            case MessageType.PING:
                break;
            case MessageType.PONG:
                break;
            case MessageType.URI:
                break;
            case MessageType.SYNC:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Stop()
    {
        Server?.Stop();
        Client?.Stop();
    }
    
    public void SetBufferSize(int size)
    {
        MaxBufferSize = size;
        Server.MaxBufferSize = size;
        Client.MaxBufferSize = size;
    }
}
}