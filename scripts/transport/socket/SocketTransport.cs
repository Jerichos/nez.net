using System;
using System.Net;
using System.Net.Sockets;
using ZeroFormatter;

public class SocketTransport : ITransport
{
    private Socket _serverSocket;
    private Socket _clientSocket;
    private IPEndPoint _ipEndPoint;

    // Initialize the server socket and start listening
    public void InitializeServer(int port)
    {
        _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        _serverSocket.Bind(_ipEndPoint);
        _serverSocket.Listen(10);
    }

    // Initialize the client socket and connect to the server
    public void InitializeClient(string ipAddress, int port)
    {
        _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _clientSocket.Connect(ipAddress, port);
    }

    public void ServerSend(NetMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        _serverSocket.Send(serializedMessage);
    }

    public NetMessage ServerReceive()
    {
        byte[] buffer = new byte[1024];
        int receivedLength = _serverSocket.Receive(buffer);

        byte[] actualReceived = new byte[receivedLength];
        Array.Copy(buffer, actualReceived, receivedLength);

        return ZeroFormatterSerializer.Deserialize<NetMessage>(actualReceived);
    }

    public void ClientSend(NetMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        _clientSocket.Send(serializedMessage);
    }

    public NetMessage ClientReceive()
    {
        byte[] buffer = new byte[1024];
        int receivedLength = _clientSocket.Receive(buffer);

        byte[] actualReceived = new byte[receivedLength];
        Array.Copy(buffer, actualReceived, receivedLength);

        return ZeroFormatterSerializer.Deserialize<NetMessage>(actualReceived);
    }

    public void CloseServer()
    {
        _serverSocket.Close();
    }

    public void CloseClient()
    {
        _clientSocket.Close();
    }
}
