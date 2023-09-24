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

    public int MaxBufferSize { get; set; } = 512; // 2kb, default was 1024
    
    public SocketTransport()
    {
        Server = new SocketServer(MaxBufferSize);
        Client = new SocketClient(MaxBufferSize);
        
        // NetworkMessage.PreCalculateBufferSizes();
        
        //Formatter<DefaultResolver, Dictionary<Guid, NetworkIdentity>>.Register(new NetworkStateFormatter<DefaultResolver>());
        Formatter<DefaultResolver, NetworkIdentity>.Register(new NetworkIdentityFormatter<DefaultResolver>());
        Formatter<DefaultResolver, NetworkComponent>.Register(new NetworkComponentFormatter<DefaultResolver>());

        // ZeroFormatterSerializer.Serialize(new TransportMessage{Code = (byte)TransportCode.CLIENT_ERROR});
    }

    public void Stop()
    {
        Server?.Stop();
        Client?.Stop();
    }
}
}