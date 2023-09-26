using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Nez;
using nez.net.components;
using ZeroFormatter;

namespace nez.net.transport.socket;

public class SocketServer : SocketHandler, ISocketServerHandler
{
    public override bool IsRunning => Socket != null && Socket.IsBound;
    public int MaxConnections { get; set; } = 10;

    private IPEndPoint _ipEndPoint;
    
    private readonly Dictionary<ushort, Socket> _clientSockets = new();
    private readonly Dictionary<Socket, ushort> _clientIDs = new();
    
    public SocketServer(int bufferSize, NetworkState networkState) : base(bufferSize, networkState) { }

    public override void Stop()
    {
        StopServer();
    }

    // Initialize the server socket and start listening
    public void Start(int port)
    {
        if (IsRunning)
        {
            Debug.Warn("server is already running");
            return;
        }
        
        Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        Socket.Bind(_ipEndPoint);
        Socket.Listen(10);
        Socket.BeginAccept(OnClientConnected, null);
        
        IsClosing = false;
    }

    private void StopServer()
    {
        if (!IsRunning)
        {
            Debug.Log("server is not running");
            return;
        }

        if (IsClosing)
        {
            Debug.Log("server is already closing");
            return;
        }
        
        IsClosing = true;
        
        foreach (var clientSocket in _clientSockets)
        {
            clientSocket.Value.Close();
        }
        
        Console.WriteLine("stopping server");
        
        _clientSockets.Clear();
        _clientIDs.Clear();

        Socket.Close();
        Socket = null;
    }
    
    private void OnClientConnected(IAsyncResult ar)
    {
        if (IsClosing)
            return;

        // Check if maximum connections reached
        if (_clientSockets.Count >= MaxConnections)
        {
            // Optionally log a warning or send a message to the client
            Debug.Warn("Maximum number of clients reached. Cannot accept more clients.");
        
            // End the pending accept operation without adding the new client
            // This is crucial as failing to end the operation could lead to resource leaks
            Socket tempSocket = Socket.EndAccept(ar);
            
            // TODO: this causes exception, because clientID is not found for this socket connection
            SendWithoutMessageID(tempSocket, new TransportMessage{Code = TransportCode.MAXIMUM_CONNECTION_REACHED});
            tempSocket.Close();
        }
        else
        {
            Socket clientSocket = Socket.EndAccept(ar);
            int clientID = (ushort)_clientSockets.Count + 1;

            if (clientID > ushort.MaxValue)
                throw new Exception("Maximum number of clients reached. Cannot accept more clients.");

            _clientSockets.Add((ushort)clientID, clientSocket);
            _clientIDs.Add(clientSocket, (ushort)clientID);

            byte[] buffer = new byte[MaxBufferSize];
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(clientSocket, buffer));
            OnClientConnected((ushort)clientID);
        }

        // Continue accepting more clients.
        Socket.BeginAccept(OnClientConnected, null);
    }

    protected override ushort GetConnectionID(Socket connection)
    {
        Console.WriteLine($"getting connection id. Current connection IDs: {_clientIDs.Count}");
        
        // throw exception if there is no connection
        if (!_clientIDs.ContainsKey(connection))
        {
            // TODO: find a way to handle this
            return ushort.MaxValue;
        }
        
        return _clientIDs[connection];
    }

    public override void Send(NetworkMessage message)
    {
        Console.WriteLine($"sending message to all clients message: {message.Type}");
        foreach (var clientSocket in _clientSockets)
        {
            Send(clientSocket.Value, message);
        }
    }

    public void Send(ushort clientID, NetworkMessage message)
    {
        Console.WriteLine($"sending message to client {clientID} message: {message.Type}");
        Send(_clientSockets[clientID], message);
    }

    public void OnClientConnected(ushort clientId)
    {
        Console.WriteLine($"client connected: {clientId}. Sending network state.");
        
        NetworkStateMessage networkStateMessage = new NetworkStateMessage
        {
            // MessageId = 9999,
            NetworkEntities = new Dictionary<Guid, NetworkIdentity>(NetworkState.GetNetworkEntities()),
            NetworkComponents = new Dictionary<Guid, NetworkComponent>(NetworkState.GetNetworkComponents())
        };

        var serMessage = ZeroFormatterSerializer.Serialize(networkStateMessage);
        var desMessage = ZeroFormatterSerializer.Deserialize<NetworkStateMessage>(serMessage);
        
        Send(clientId, networkStateMessage);
    }

    public void GetNetworkState(out ConcurrentDictionary<Guid, NetworkIdentity> networkEntities, out ConcurrentDictionary<Guid, NetworkComponent> networkComponents)
    {
        networkEntities = NetworkState.GetNetworkEntities();
        networkComponents = NetworkState.GetNetworkComponents();
    }
}