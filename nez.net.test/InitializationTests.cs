using nez.net.transport.socket;

namespace nez.net.test;

[TestFixture]
public class InitializationTests
{
    private SocketTransport _serverTransport;
    private SocketTransport _clientTransport;

    // Add test initialization
    [SetUp]
    public void Setup()
    {
        _serverTransport = new SocketTransport();
        _clientTransport = new SocketTransport();
    }
    
    // Add test cleanup
    [TearDown]
    public void TearDown()
    {
        _serverTransport.Stop();
        _clientTransport.Stop();
    }
    
    [Test]
    public void TestServerInitialization()
    {
        // Act
        _serverTransport.Server.Start(5000);
    
        // Assert
        // Replace the assertion below with the actual validation.
        // For example, you might want to check a boolean that confirms the server is running.
        Assert.IsTrue(_serverTransport.IsServerRunning);
        
        _serverTransport.Server.Stop();
    }
    
    [Test]
    public void TestClientInitialization()
    {
        // Act
        _serverTransport.Server.Start(5000);
        Thread.SpinWait(10);
        _clientTransport.Client.Start("127.0.0.1", 5000);
        Thread.SpinWait(10);
    
        // Assert
        // Replace the assertion below with the actual validation.
        // For example, you could check if the client has a valid socket connection.
        Assert.IsTrue(_clientTransport.IsClientRunning);
        
        _serverTransport.Server.Stop();
        _clientTransport.Client.Stop();
    }
}