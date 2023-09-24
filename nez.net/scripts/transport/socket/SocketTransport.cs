using System;
using System.Collections.Generic;
using nez.net.components;
using ZeroFormatter;
using ZeroFormatter.Formatters;

namespace nez.net.transport.socket
{
public delegate void Delegate<in T>(T arg);

public class SocketTransport
{
    public ISocketServerHandler Server { get; }
    public ISocketClientHandler Client { get; }

    public bool IsServerRunning => Server.IsRunning;
    public bool IsClientRunning => Client.IsRunning;

    public int ReceiveBufferSize { get; set; } = 2048; // 2kb, default was 1024
    public int SendBufferSize { get; set; } = 2048; // 2kb, default was 1024
    
    public SocketTransport()
    {
        Server = new SocketServer(ReceiveBufferSize, SendBufferSize);
        Client = new SocketClient(ReceiveBufferSize, SendBufferSize);
        
        NetworkMessage.PreCalculateBufferSizes();
        
        Formatter<DefaultResolver, Dictionary<Guid, NetworkIdentity>>.Register(new NetworkStateFormatter<DefaultResolver>());
        Formatter<DefaultResolver, NetworkIdentity>.Register(new NetworkIdentityFormatter<DefaultResolver>());
        Formatter<DefaultResolver, NetworkComponent>.Register(new NetworkComponentFormatter<DefaultResolver>());

        Formatter<DefaultResolver, Uri>.Register(new UriFormatter<DefaultResolver>());
        ZeroFormatterSerializer.Serialize(new TransportMessage());
    }

    public void Stop()
    {
        Server?.Stop();
        Client?.Stop();
    }
}
}