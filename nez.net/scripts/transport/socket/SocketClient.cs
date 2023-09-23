using System;
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
                byte[] buffer = new byte[1024];
                _clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ClientReceiveCallback, Tuple.Create(buffer, _clientSocket));
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

        _isClosing = false;
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

    private void ClientReceiveCallback(IAsyncResult ar)
    {
        if(!IsRunning || _isClosing)
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
                RaiseEvent(OnTransportMessage, transportMessage.Code);
                
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
            
            if (message != null)
            {
                RaiseEvent(OnReceive, message);
                
                switch (message.Type)
                {
                    case MessageType.NETWORK_STATE:
                        var gameStateMessage = (NetworkStateMessage)message;
                        // Process game state message
                        NetworkState.Instance.SetNetworkState(gameStateMessage.NetworkEntities, gameStateMessage.NetworkComponents);
                        break;
                    case MessageType.TRANSPORT:
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
            }

            // You can add additional logic to process the message here if needed
            // Start another receive operation
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ClientReceiveCallback, state);
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