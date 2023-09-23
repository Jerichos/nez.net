using Nez;
using nez.net.transport.socket;

namespace nez.net.test;

[TestFixture]
public class NetworkStateTest
{
    private SocketTransport _serverSocket;
    private SocketTransport _clientSocket;

    private readonly int _port = 7777;

    private Scene _serverScene;
    private Scene _clientScene;
    
    [SetUp]
    public void Setup()
    {
        _serverSocket = new SocketTransport();
        _clientSocket = new SocketTransport();
        
        _serverSocket.Server.Start(_port);
        Thread.SpinWait(10);
    }

    [TearDown]
    public void TearDown()
    {
        _serverSocket.Stop();
        _clientSocket.Stop();
    }

    [Test, Timeout(1000)]
    public async Task TestNetworkStateSynchronization()
    {
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        _clientSocket.Client.OnReceive += message =>
        {
                tcs.SetResult(true);
        };
        
        _clientSocket.Client.Start("127.0.0.1", _port);
        
        await tcs.Task;
        Assert.IsTrue(tcs.Task.Result);
        Assert.IsTrue(_serverSocket.IsServerRunning);
        Assert.IsTrue(_clientSocket.IsClientRunning);
    }
}