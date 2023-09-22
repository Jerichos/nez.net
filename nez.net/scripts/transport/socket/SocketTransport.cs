using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nez;
using ZeroFormatter;

namespace nez.net.transport.socket;

public delegate void Delegate<in TEventArgs>(TEventArgs arg);

public class SocketTransport : ITransport
{
    private Socket _serverSocket;
    private Socket _clientSocket;
    private IPEndPoint _ipEndPoint;

    private Dictionary<uint, Socket> _clientSockets = new();
    
    public event Delegate<NetworkMessage> EServerReceive;
    public event Delegate<NetworkMessage> EClientReceive;

    public event Delegate<TransportCode> EClientTransportChanged; 

    private bool _isClosing;

    public bool IsServer { get; private set; }
    public bool IsClient => _clientSocket != null && _clientSocket.Connected;
    public int MaxConnections { get; set; } = 10;

    public SocketTransport()
    {
        ZeroFormatterSerializer.Serialize(new NetworkMessage());
    }

    // Initialize the server socket and start listening
    public void StartServer(int port)
    {
        if (IsServer)
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
        IsServer = true;
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

    // Initialize the client socket and connect to the server
    public void ConnectClient(string ipAddress, int port)
    {
        if (IsClient)
        {
            Debug.Warn("client is already running");
            EClientTransportChanged?.Invoke(TransportCode.CLIENT_ALREADY_CONNECTED);
            return;
        }

        try
        {
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Set a timeout period
            _clientSocket.ReceiveTimeout = 5000; // 5 seconds timeout for example
            _clientSocket.SendTimeout = 5000; // 5 seconds timeout for example

            IAsyncResult result = _clientSocket.BeginConnect(ipAddress, port, null, null);

            // Wait for the connection to complete within 5 seconds
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));

            if (result.IsCompleted)
            {
                _clientSocket.EndConnect(result);
                byte[] buffer = new byte[1024];
                _clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ClientReceiveCallback, Tuple.Create(buffer, _clientSocket));
                EClientTransportChanged?.Invoke(TransportCode.CLIENT_CONNECTED);
            }
            else
            {
                _clientSocket.Close();
                Debug.Warn("connection timed out");
                EClientTransportChanged?.Invoke(TransportCode.CLIENT_CONNECTION_TIMEOUT);
            }
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.ConnectionRefused)
            {
                // Handle connection refused or max connections reached
                Debug.Warn("Connection refused by the server");
                EClientTransportChanged?.Invoke(TransportCode.CLIENT_CONNECTION_REFUSED);
            }
            else
            {
                // Handle other socket exceptions
                Debug.Warn($"SocketException: {e}");
                EClientTransportChanged?.Invoke(TransportCode.CLIENT_ERROR);
            }
        }
        catch (Exception e)
        {
            // Handle other general exceptions
            Debug.Warn($"An error occurred: {e}");
            EClientTransportChanged?.Invoke(TransportCode.CLIENT_ERROR);
        }

        _isClosing = false;
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

    public void ClientSend(NetworkMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        _clientSocket.BeginSend(serializedMessage, 0, serializedMessage.Length, SocketFlags.None, ClientSendCallback, _clientSocket);
    }

    private void ClientSendCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState;
        socket.EndSend(ar);
    }

    private void ClientReceiveCallback(IAsyncResult ar)
    {
        if(!IsClient || _isClosing)
            return;
        
        var state = (Tuple<byte[], Socket>)ar.AsyncState;
        byte[] buffer = state.Item1;
        Socket clientSocket = state.Item2;

        int receivedLength = clientSocket.EndReceive(ar);

        if (receivedLength > 0)
        {
            byte[] actualReceived = new byte[receivedLength];
            Array.Copy(buffer, actualReceived, receivedLength);
            
            TransportMessage transportMessage = ZeroFormatterSerializer.Deserialize<TransportMessage>(actualReceived);
            if (transportMessage != null)
            {
                EClientTransportChanged?.Invoke(transportMessage.Code);
                
                switch (transportMessage.Code)
                {
                    case TransportCode.MAXIMUM_CONNECTION_REACHED:
                        StopClient();
                        return;
                    default:
                        Debug.Warn("transport message not handled: " + transportMessage.Code);
                        break;
                }
            }

            NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualReceived);
            if(message != null)
                EClientReceive?.Invoke(message);

            // You can add additional logic to process the message here if needed
            // Start another receive operation
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ClientReceiveCallback, state);
        }
    }

    public void StopServer()
    {
        if (!IsServer)
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
        IsServer = false;
    }

    public void StopClient()
    {
        // if(!IsClient)
        // {
        //     Debug.Log("client is not running");
        //     return;
        // }

        if (_isClosing)
        {
            Debug.Log("client is already closing");
            return;
        }
        
        _isClosing = true;
        _clientSocket.Close();
        _clientSocket = null;
    }
}