using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Nez;
using ZeroFormatter;

namespace nez.net.transport.socket;

public class SocketClient : ISocketClientHandler
{
    public bool IsRunning => _clientSocket != null && _clientSocket.Connected;

    public event Delegate<NetworkMessage> OnReceive;
    public event Delegate<TransportCode> OnTransportMessage; 
    
    private Socket _clientSocket;
    private bool _isClosing;
    
    public int MaxBufferSize { get; set; }

    public NetworkState NetworkState { get; set; }
    
    public SocketClient(int maxBufferSize)
    {
        MaxBufferSize = maxBufferSize;
        NetworkState = new NetworkState();
    }
    
    public void Start(string address, int port)
    {
        ConnectClient(address, port);
    }
    
    public void Stop()
    {
        StopClient();
    }

   

    // Initialize the client socket and connect to the server
    private void ConnectClient(string ipAddress, int port)
    {
        if (IsRunning)
        {
            Debug.Warn("client is already running");
            OnTransportMessage?.Invoke(TransportCode.CLIENT_ALREADY_CONNECTED);
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
                byte[] buffer = new byte[MaxBufferSize];
                _clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ClientReceiveCallback, Tuple.Create(buffer, _clientSocket));
                _isClosing = false;
                OnTransportMessage?.Invoke(TransportCode.CLIENT_CONNECTED);
            }
            else
            {
                _clientSocket.Close();
                Debug.Warn("connection timed out");
                OnTransportMessage?.Invoke(TransportCode.CLIENT_CONNECTION_TIMEOUT);
            }
        }
        catch (SocketException e)
        {
            Debug.Warn($"SocketException: {e}");
            Debug.Warn($"Stack Trace: {e.StackTrace}");
            OnTransportMessage?.Invoke(TransportCode.CLIENT_ERROR);
        }
        catch (TimeoutException e)
        {
            Debug.Warn($"TimeoutException: {e}");
            Debug.Warn($"Stack Trace: {e.StackTrace}");
            OnTransportMessage?.Invoke(TransportCode.CLIENT_CONNECTION_TIMEOUT);
        }
        catch (Exception e)
        {
            Debug.Warn($"An unknown error occurred: {e}");
            Debug.Warn($"Stack Trace: {e.StackTrace}");
            OnTransportMessage?.Invoke(TransportCode.CLIENT_ERROR);
        }
    }
        
    public void Send(NetworkMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        _clientSocket.BeginSend(serializedMessage, 0, serializedMessage.Length, SocketFlags.None, ClientSendCallback, _clientSocket);
    }

    private void ClientSendCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState;
        socket.EndSend(ar);
    }
    
    private readonly Dictionary<Socket, List<byte>> _incompleteMessages = new();

    private void ClientReceiveCallback(IAsyncResult ar)
    {
        if(!IsRunning || _isClosing)
            return;
        
        var state = (Tuple<byte[], Socket>)ar.AsyncState;
        byte[] buffer = state.Item1;
        Socket senderSocket = state.Item2;

        int receivedLength = senderSocket.EndReceive(ar);

        if (receivedLength > 0)
        {
            if (!_incompleteMessages.ContainsKey(senderSocket))
            {
                _incompleteMessages[senderSocket] = new List<byte>();
            }
            
            _incompleteMessages[senderSocket].AddRange(new ArraySegment<byte>(buffer, 0, receivedLength));

            try
            {
                NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(_incompleteMessages[senderSocket].ToArray());
                if (message != null)
                {
                    switch (message.Type)
                    {
                        case MessageType.NETWORK_STATE:
                            var gameStateMessage = (NetworkStateMessage)message;
                            
                            // Process game state message
                            NetworkState.SetNetworkState(gameStateMessage.NetworkEntities, gameStateMessage.NetworkComponents);
                            break;
                        case MessageType.TRANSPORT:
                            var transportMessage = (TransportMessage)message;
                            RaiseEvent(OnTransportMessage, TransportCode.MAXIMUM_CONNECTION_REACHED);
                            switch (transportMessage.Code)
                            {
                                case TransportCode.MAXIMUM_CONNECTION_REACHED:
                                    StopClient();
                                    return;
                                default:
                                    Debug.Warn("transport message not handled: " + transportMessage.Code);
                                    break;
                            }
                            break;
                        case MessageType.MIRROR:
                            break;
                        case MessageType.PING:
                            break;
                        case MessageType.PONG:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    
                    _incompleteMessages[senderSocket].Clear();
                    RaiseEvent(OnReceive, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Message is probably incomplete, waiting for more data...");
            }
            
            // Start another receive operation
            senderSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ClientReceiveCallback, state);
        }
    }

    private void StopClient()
    {
        if (_isClosing)
        {
            Debug.Log("client is already closing");
            return;
        }
        
        if (!IsRunning)
        {
            Debug.Log("client is not running");
            return;
        }
        
        _isClosing = true;
        _clientSocket.Close();
        _clientSocket = null;
    }
    
    // Safely invoke an event
    protected void RaiseEvent<T>(Delegate<T> eventToRaise, T arg)
    {
        Delegate<T> handler = eventToRaise;
        handler?.Invoke(arg);
    }
}