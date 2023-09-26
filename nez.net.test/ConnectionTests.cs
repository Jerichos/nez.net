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
    
    private SocketTransport _serverTransport;
    private SocketTransport[] _clientTransports;
    private SocketTransport _overLimitClient;
    private SocketTransport _clientTransport;

    readonly int _maxConnections = 3; // Set your server's max connection limit

    [SetUp]
    public void Setup()
    {
        // instantiate all transports
        _serverTransport = new SocketTransport();
        _clientTransports = new SocketTransport[_maxConnections];
        for (int i = 0; i < _maxConnections; i++)
        {
            _clientTransports[i] = new SocketTransport();
        }
        _overLimitClient = new SocketTransport();
        _clientTransport = new SocketTransport();
    }
    
    [TearDown]
    public void TearDown()
    {
        // stop all transports
        _serverTransport?.Server?.Stop();
        for (int i = 0; i < _clientTransports.Length; i++)
        {
            _clientTransports[i]?.Client?.Stop();
        }
        _overLimitClient?.Client?.Stop();
        _clientTransport?.Client?.Stop();
    }

    [Test, Timeout(1000)]
    public async Task TestMaximumConnections()
    {
        _serverTransport.Server.Start(5000);
        _serverTransport.Server.MaxConnections = _maxConnections;
        Thread.SpinWait(10);
            
        // Act
        for (int i = 0; i < _maxConnections; i++)
        {
            _clientTransports[i].Client.Start("127.0.0.1", 5000);
            Thread.SpinWait(10);
        }
            
        _clientConnectCallbackCompleted = new TaskCompletionSource<bool>();

        _overLimitClient.Client.OnMessageReceived += (socket, message) =>
        {
            if (message is TransportMessage transportMessage)
            {
                if (transportMessage.Code == TransportCode.MAXIMUM_CONNECTION_REACHED)
                {
                    _clientConnectCallbackCompleted.SetResult(false);
                }
            }
        };
        
        _overLimitClient.Client.Start("127.0.0.1", 5000);
            
        await Task.WhenAll(_clientConnectCallbackCompleted.Task);
        // Assert
        // Check if overLimitConnectionResult is false or another way to determine if the connection failed due to exceeding the limit.
        Assert.IsFalse(_clientConnectCallbackCompleted.Task.Result);

        // Cleanup
        _serverTransport.Server.Stop();
        for (int i = 0; i < _maxConnections; i++)
        {
            _clientTransports[i].Client.Stop();
        }
        _overLimitClient.Client.Stop();
    }

    [Test]
    public async Task TestFailedConnections()
    {
        // Act
        _clientConnectCallbackCompleted = new TaskCompletionSource<bool>();

        _clientTransport.Client.OnMessageReceived += (connection, message) =>
        {
            if(message is TransportMessage transportMessage)
            {
                if (transportMessage.Code != TransportCode.CLIENT_CONNECTED)
                {
                    _clientConnectCallbackCompleted.SetResult(true);
                }
            }
        };
        
        _clientTransport.Client.Start("127.0.0.1", 1234);
        
        
        await Task.WhenAll(_clientConnectCallbackCompleted.Task);
        
        // Assert
        Assert.IsFalse(_clientConnectCallbackCompleted.Task.Result);
    }
}
}