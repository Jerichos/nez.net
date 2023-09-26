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

        NetworkMessage message = new PingMessage();
        var ping = ZeroFormatterSerializer.Serialize(message);
        var deserialized = ZeroFormatterSerializer.Deserialize<NetworkMessage>(ping);
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