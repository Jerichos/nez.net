using nez.net.transport.socket;

namespace nez.net.test;

[TestFixture]
public class InitializationTests
{
    [Test]
    public void TestServerInitialization()
    {
        // Arrange
        SocketTransport socketTransport = new SocketTransport();

        // Act
        socketTransport.Server.Start(5000);
    
        // Assert
        // Replace the assertion below with the actual validation.
        // For example, you might want to check a boolean that confirms the server is running.
        Assert.IsTrue(socketTransport.IsServerRunning);
        
        socketTransport.Server.Stop();
    }
    
    [Test]
    public void TestClientInitialization()
    {
        // Arrange
        SocketTransport serverTransport = new SocketTransport();
        SocketTransport clientTransport = new SocketTransport();

        // Act
        serverTransport.Server.Start(5000);
        Thread.SpinWait(10);
        clientTransport.Client.Start("127.0.0.1", 5000);
        Thread.SpinWait(10);
    
        // Assert
        // Replace the assertion below with the actual validation.
        // For example, you could check if the client has a valid socket connection.
        Assert.IsTrue(clientTransport.IsClientRunning);
        
        serverTransport.Server.Stop();
        clientTransport.Client.Stop();
    }
}