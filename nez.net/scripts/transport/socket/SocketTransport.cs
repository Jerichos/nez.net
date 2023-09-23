using System;
using System.Collections.Concurrent;
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
    
    public SocketTransport()
    {
        Server = new SocketServer();
        Client = new SocketClient();
        
        Formatter<DefaultResolver, Dictionary<Guid, NetworkIdentity>>.Register(new NetworkStateFormatter<DefaultResolver>());
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