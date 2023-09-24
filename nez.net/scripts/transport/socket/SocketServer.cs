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
    
    private readonly Dictionary<uint, Socket> _clientSockets = new();
    private readonly Dictionary<Socket, uint> _clientIDs = new();
    
    public SocketServer(int bufferSize, NetworkState networkState)
    {
        MaxBufferSize = bufferSize;
        NetworkState = networkState;
    }

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
            
            // notify client that maximum connections reached
            Send(tempSocket, new TransportMessage{Code = TransportCode.MAXIMUM_CONNECTION_REACHED});
            tempSocket.Close();
        }
        else
        {
            Socket clientSocket = Socket.EndAccept(ar);
            uint clientID = (uint)_clientSockets.Count + 1;
            
            _clientSockets.Add(clientID, clientSocket);
            _clientIDs.Add(clientSocket, clientID);

            byte[] buffer = new byte[MaxBufferSize];
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(clientSocket, buffer));
            OnClientConnected(clientID);
        }

        // Continue accepting more clients.
        Socket.BeginAccept(OnClientConnected, null);
    }
    
    public override void Send(NetworkMessage message)
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
            // MessageId = 9999,
            NetworkEntities = new Dictionary<Guid, NetworkIdentity>(NetworkState.GetNetworkEntities()),
            NetworkComponents = new Dictionary<Guid, NetworkComponent>(NetworkState.GetNetworkComponents())
        };
        
        // Send(clientId, networkStateMessage);
    }

    public void GetNetworkState(out ConcurrentDictionary<Guid, NetworkIdentity> networkEntities, out ConcurrentDictionary<Guid, NetworkComponent> networkComponents)
    {
        networkEntities = NetworkState.GetNetworkEntities();
        networkComponents = NetworkState.GetNetworkComponents();
    }
}