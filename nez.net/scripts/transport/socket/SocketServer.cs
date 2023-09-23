using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Nez;
using ZeroFormatter;

namespace nez.net.transport.socket;

public class SocketServer
{
    public bool IsRunning => _serverSocket != null && _serverSocket.IsBound;
    public int MaxConnections { get; set; } = 10;
    
    public event Delegate<NetworkMessage> EServerReceive;
    
    private Socket _serverSocket;
    private IPEndPoint _ipEndPoint;
    private Dictionary<uint, Socket> _clientSockets = new();
    
    private bool _isClosing;
    
    // Initialize the server socket and start listening
    public void StartServer(int port)
    {
        if (IsRunning)
        {
            Debug.Warn("server is already running");
            return;
        }
        
        _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        _serverSocket.Bind(_ipEndPoint);
        _serverSocket.Listen(10);
        _serverSocket.BeginAccept(ServerAcceptCallback, null);
        
        _isClosing = false;
    }
    
    public void StopServer()
    {
        if (!IsRunning)
        {
            Debug.Log("server is not running");
            return;
        }

        if (_isClosing)
        {
            Debug.Log("server is already closing");
            return;
        }
        
        _isClosing = true;
        
        foreach (var clientSocket in _clientSockets)
        {
            clientSocket.Value.Close();
        }

        _serverSocket.Close();
        _serverSocket = null;
    }
    
    private void ServerAcceptCallback(IAsyncResult ar)
    {
        if (_isClosing)
            return;

        // Check if maximum connections reached
        if (_clientSockets.Count >= MaxConnections)
        {
            // Optionally log a warning or send a message to the client
            Debug.Warn("Maximum number of clients reached. Cannot accept more clients.");
        
            // End the pending accept operation without adding the new client
            // This is crucial as failing to end the operation could lead to resource leaks
            Socket tempSocket = _serverSocket.EndAccept(ar);
            // Send a message to the client that the server is full
            
            byte[] msg = ZeroFormatterSerializer.Serialize(new TransportMessage { Code = TransportCode.MAXIMUM_CONNECTION_REACHED });
            tempSocket.Send(msg);
            
            tempSocket.Close();
        }
        else
        {
            Socket clientSocket = _serverSocket.EndAccept(ar);
            uint clientID = (uint)_clientSockets.Count + 1;
            _clientSockets.Add(clientID, clientSocket);

            byte[] buffer = new byte[1024];
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ServerReceiveCallback, Tuple.Create(clientSocket, buffer));
        }

        // Continue accepting more clients.
        _serverSocket.BeginAccept(ServerAcceptCallback, null);
    }
    
    public void ServerSend(NetworkMessage message)
    {
        foreach (var clientSocket in _clientSockets)
        {
            ServerSend(message, clientSocket.Value);
        }
    }

    public void ServerSend(NetworkMessage message, uint clientID)
    {
        ServerSend(message, _clientSockets[clientID]);
    }

    public void ServerSend(NetworkMessage message, Socket clientSocket)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        clientSocket.BeginSend(serializedMessage, 0, serializedMessage.Length, SocketFlags.None, new AsyncCallback(SendCallback), clientSocket);
    }
    
    private void SendCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState;
        socket.EndSend(ar);
    }
    
    private void ServerReceiveCallback(IAsyncResult ar)
    {
        if(_isClosing)
            return;
        
        var state = (Tuple<Socket, byte[]>)ar.AsyncState;
        Socket clientSocket = state.Item1;
        byte[] buffer = state.Item2;

        int receivedLength = clientSocket.EndReceive(ar);

        if (receivedLength > 0)
        {
            byte[] actualReceived = new byte[receivedLength];
            Array.Copy(buffer, actualReceived, receivedLength);

            NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualReceived);
            EServerReceive?.Invoke(message);

            // Process the received message
            // For example, you can add it to a queue for another component to handle,
            // or directly process the message here based on its type.
            
            // Start another receive operation
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ServerReceiveCallback, state);
        }
    }
}