using System;
using System.Net.Sockets;
using Nez;
using ZeroFormatter;

namespace nez.net.transport.socket;

public class SocketClient
{
    public bool IsRunning => _clientSocket != null && _clientSocket.Connected;
    
    public event Delegate<NetworkMessage> EClientReceive;
    public event Delegate<TransportCode> EClientTransportChanged; 
    
    private Socket _clientSocket;
    private bool _isClosing;
    
    // Initialize the client socket and connect to the server
    public void ConnectClient(string ipAddress, int port)
    {
        if (IsRunning)
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