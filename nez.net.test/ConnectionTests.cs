using nez.net.transport.socket;
using System.Threading;
using nez.net.transport;
using NUnit.Framework;

namespace nez.net.test
{

[TestFixture]
public class ConnectionTests
{
    private TaskCompletionSource<bool> _clientConnectCallbackCompleted;

    [Test, Timeout(1000)]
    public async Task TestMaximumConnections()
    {
        // Arrange
        SocketTransport serverTransport = new SocketTransport();
        int maxConnections = 10; // Set your server's max connection limit
            
        serverTransport.StartServer(5000);
        serverTransport.Server.MaxConnections = maxConnections;
        Thread.SpinWait(10);
            
        SocketTransport[] clientTransports = new SocketTransport[maxConnections];
            
        // Act
        for (int i = 0; i < maxConnections; i++)
        {
            clientTransports[i] = new SocketTransport();
            clientTransports[i].ConnectClient("127.0.0.1", 5000);
            Thread.SpinWait(10);
        }
            
        _clientConnectCallbackCompleted = new TaskCompletionSource<bool>();
        
        SocketTransport overLimitClient = new SocketTransport();
        overLimitClient.Client.EClientTransportChanged += code =>
        {
            if (code == TransportCode.MAXIMUM_CONNECTION_REACHED)
                _clientConnectCallbackCompleted.SetResult(false);
        };
        
        overLimitClient.ConnectClient("127.0.0.1", 5000);
            
        await Task.WhenAll(_clientConnectCallbackCompleted.Task);
        // Assert
        // Check if overLimitConnectionResult is false or another way to determine if the connection failed due to exceeding the limit.
        Assert.IsFalse(_clientConnectCallbackCompleted.Task.Result);

        // Cleanup
        serverTransport.StopServer();
        for (int i = 0; i < maxConnections; i++)
        {
            clientTransports[i].StopClient();
        }
        overLimitClient.StopClient();
    }

    [Test]
    public async Task TestFailedConnections()
    {
        // Arrange
        SocketTransport clientTransport = new SocketTransport();

        // Act
        _clientConnectCallbackCompleted = new TaskCompletionSource<bool>();

        clientTransport.Client.EClientTransportChanged += code =>
        {
            if (code != TransportCode.CLIENT_CONNECTED)
                _clientConnectCallbackCompleted.SetResult(false);
            else
                _clientConnectCallbackCompleted.SetResult(true);
        };
        
        clientTransport.ConnectClient("127.0.0.1", 1234);
        
        
        await Task.WhenAll(_clientConnectCallbackCompleted.Task);
        
        // Assert
        Assert.IsFalse(_clientConnectCallbackCompleted.Task.Result);

        // Cleanup
        clientTransport.StopClient();
    }
}
}