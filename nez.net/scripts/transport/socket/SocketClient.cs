using System;
using System.Net.Sockets;
using Nez;

namespace nez.net.transport.socket;

public class SocketClient : SocketHandler, ISocketClientHandler
{
    public SocketClient(int maxBufferSize, NetworkState networkState) : base(maxBufferSize, networkState){ }
    
    public void Start(string address, int port)
    {
        ConnectClient(address, port);
    }
    
    // Initialize the client socket and connect to the server
    private void ConnectClient(string ipAddress, int port)
    {
        TransportCode code = TransportCode.CLIENT_ERROR;
        if (IsRunning)
        {
            Debug.Warn("client is already running");
            
            return;
        }
        try
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Set a timeout period
            Socket.ReceiveTimeout = 5000; // 5 seconds timeout for example
            Socket.SendTimeout = 5000; // 5 seconds timeout for example

            IAsyncResult result = Socket.BeginConnect(ipAddress, port, null, null);

            // Wait for the connection to complete within 5 seconds
            result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));

            if (result.IsCompleted)
            {
                Socket.EndConnect(result);
                byte[] buffer = new byte[MaxBufferSize];
                Socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(Socket, buffer));
                IsClosing = false;
                code = TransportCode.CONNECTED;
            }
            else
            {
                Socket.Close();
                Debug.Warn("connection timed out");
                code = TransportCode.CONNECTION_TIMETOUT;
            }
        }
        catch (SocketException e)
        {
            Debug.Warn($"SocketException: {e}");
            Debug.Warn($"Stack Trace: {e.StackTrace}");
            code = TransportCode.CONNECTION_ERROR;
        }
        catch (TimeoutException e)
        {
            Debug.Warn($"TimeoutException: {e}");
            Debug.Warn($"Stack Trace: {e.StackTrace}");
            code = TransportCode.CONNECTION_TIMETOUT;
        }
        catch (Exception e)
        {
            Debug.Warn($"An unknown error occurred: {e}");
            Debug.Warn($"Stack Trace: {e.StackTrace}");
            code = TransportCode.CLIENT_ERROR;
        }
        
        RaiseEvent(OnMessageReceived, Socket, new TransportMessage {Code = code});
    }

    protected override ushort GetConnectionID(Socket connection)
    {
        return 0;
    }
}