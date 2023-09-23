using ZeroFormatter;

namespace nez.net.transport.socket;

public delegate void Delegate<in T>(T arg);

public class SocketTransport
{
    public SocketServer Server { get; private set; }
    public SocketClient Client { get; private set; }
    
    public bool IsServerRunning => Server.IsRunning;
    public bool IsClientRunning => Client.IsRunning;

    public SocketTransport()
    {
        Server = new SocketServer();
        Client = new SocketClient();
        ZeroFormatterSerializer.Serialize(new NetworkMessage());
    }

    public void StartServer(int port)
    {
        Server.StartServer(port);
    }
    
    public void StopServer()
    {
        Server?.StopServer();
    }

    public void ConnectClient(string address, int port)
    {
        Client.ConnectClient(address, port);
    }

    public void StopClient()
    {
        Client?.StopClient();
    }
}