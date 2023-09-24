using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Nez;
using nez.net.components;
using ZeroFormatter;

namespace nez.net.transport.socket;

public class SocketServer : ISocketServerHandler
{
    public bool IsRunning => _serverSocket != null && _serverSocket.IsBound;
    public int MaxConnections { get; set; } = 10;

    public event Delegate<NetworkMessage> OnReceive;
    public event Delegate<TransportCode> OnTransportMessage;

    private Socket _serverSocket;
    private IPEndPoint _ipEndPoint;
    private readonly Dictionary<uint, Socket> _clientSockets = new();
    private readonly Dictionary<Socket, uint> _clientIDs = new();
    private bool _isClosing;
    
    private readonly int _sendBufferSize;
    private readonly int _receiveBufferSize;

    public SocketServer(int receiveBufferSize, int sendBufferSize)
    {
        _receiveBufferSize = receiveBufferSize;
        _sendBufferSize = sendBufferSize;

        NetworkState = new NetworkState();
    }

    public void Stop()
    {
        StopServer();
    }
    
    public NetworkState NetworkState { get; set; }
    
    // Initialize the server socket and start listening
    public void Start(int port)
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


    private void StopServer()
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
        
        _clientSockets.Clear();
        _clientIDs.Clear();

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
            _clientIDs.Add(clientSocket, clientID);

            byte[] buffer = new byte[_receiveBufferSize];
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ServerReceiveCallback, Tuple.Create(clientSocket, buffer));
            
            OnClientConnected(clientID);
        }

        // Continue accepting more clients.
        _serverSocket.BeginAccept(ServerAcceptCallback, null);
    }
    
    public void Send(NetworkMessage message)
    {
        foreach (var clientSocket in _clientSockets)
        {
            Send(clientSocket.Value, message);
        }
    }

    public void Send(uint clientID, NetworkMessage message)
    {
        Send(_clientSockets[clientID], message);
    }

    public void OnClientConnected(uint clientId)
    {
        NetworkStateMessage networkStateMessage = new NetworkStateMessage
        {
            NetworkEntities = new Dictionary<Guid, NetworkIdentity>(NetworkState.GetNetworkEntities()),
            NetworkComponents = new Dictionary<Guid, NetworkComponent>(NetworkState.GetNetworkComponents())
        };
        
        Send(clientId, networkStateMessage);
    }

    private void Send(Socket clientSocket, NetworkMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        
        if(serializedMessage.Length > _sendBufferSize)
        {
            // Handle message size exceeding buffer size.
            // You can either break it into smaller messages, or resize the buffer.
            Debug.Warn($"Message size {serializedMessage.Length} exceeds buffer size {_sendBufferSize}. Consider resizing the buffer or breaking the message into smaller parts.");
            return;
        }
        
        clientSocket.BeginSend(serializedMessage, 0, serializedMessage.Length, SocketFlags.None, SendCallback, clientSocket);
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

            NetworkMessage received = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualReceived);

            switch (received.Type)
            {
                case MessageType.TRANSPORT:
                    var transportMessage = (TransportMessage)received;
                    // Process transport message
                    break;
                case MessageType.MIRROR:
                    var mirrorMessage = (MirrorMessage)received;
                    Send(clientSocket, mirrorMessage);
                    // Process mirror message
                    break;
            }

            RaiseEvent(OnReceive, received);

            // Start another receive operation
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ServerReceiveCallback, state);
        }
    }
    
    // Safely invoke an event
    protected virtual void RaiseEvent<T>(Delegate<T> eventToRaise, T arg)
    {
        Delegate<T> handler = eventToRaise;
        handler?.Invoke(arg);
    }
}