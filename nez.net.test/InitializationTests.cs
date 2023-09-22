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
        socketTransport.StartServer(5000);
    
        // Assert
        // Replace the assertion below with the actual validation.
        // For example, you might want to check a boolean that confirms the server is running.
        Assert.IsTrue(socketTransport.IsServer);
        
        socketTransport.StopServer();
    }
    
    [Test]
    public void TestClientInitialization()
    {
        // Arrange
        SocketTransport serverTransport = new SocketTransport();
        SocketTransport clientTransport = new SocketTransport();

        // Act
        serverTransport.StartServer(5000);
        Thread.SpinWait(10);
        clientTransport.ConnectClient("127.0.0.1", 5000);
        Thread.SpinWait(10);
    
        // Assert
        // Replace the assertion below with the actual validation.
        // For example, you could check if the client has a valid socket connection.
        Assert.IsTrue(clientTransport.IsClient);
        
        serverTransport.StopServer();
        clientTransport.StopClient();
    }
}