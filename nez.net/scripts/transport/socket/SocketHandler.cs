using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Nez;
using ZeroFormatter;

namespace nez.net.transport.socket;

public abstract class SocketHandler : ISocketHandler
{
    public virtual bool IsRunning => Socket != null && Socket.Connected;
    public int MaxBufferSize { get; set; }
    
    protected bool IsClosing;
    
    public NetworkState NetworkState { get; set; }
    
    public Delegate<NetworkMessage> OnMessageReceived { get; set; }
    public Delegate<TransportCode> OnTransportMessage { get; set; }
    
    private readonly Dictionary<Socket, List<byte>> _incompleteMessages = new();
    
    protected Socket Socket;

    protected void HandleReceive(IAsyncResult ar)
    {
        if(!IsRunning || IsClosing)
            return;
        
        var state = (Tuple<Socket, byte[]>)ar.AsyncState;
        Socket connection = state.Item1;
        byte[] buffer = state.Item2;
        
        int receivedLength = connection.EndReceive(ar);
        
        if (receivedLength > 0)
        {
            int totalChunks = BitConverter.ToInt32(buffer, buffer.Length - 4);

            if (totalChunks == 0)
            {
                var actualMessage = new ArraySegment<byte>(buffer, 0, receivedLength).ToArray();
                NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
                HandleMessage(connection, message);
            }
            else
            {
                int chunkIndex = BitConverter.ToInt32(buffer, buffer.Length - 8);
                
                if (!_incompleteMessages.ContainsKey(connection))
                {
                    _incompleteMessages[connection] = new List<byte>();
                }
                
                // Append this chunk to the buffer
                _incompleteMessages[connection].AddRange(new ArraySegment<byte>(buffer, 0, receivedLength - 8));
                if (AllChunksReceived(connection, totalChunks, chunkIndex))
                {
                    try
                    {
                        byte[] actualMessage = _incompleteMessages[connection].ToArray();
                        // Deserialize the actual message
                        NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
            
                        // Handle the message and clear the buffer
                        HandleMessage(connection, message);
                        _incompleteMessages[connection].Clear();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        Console.WriteLine("Message is probably incomplete, waiting for more data...");
                    }
                }
            }
            
            // Start another receive operation
            if(IsClosing || !IsRunning)
                return;
            
            byte[] newBuffer = new byte[MaxBufferSize];
            connection.BeginReceive(newBuffer, 0, newBuffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(connection, newBuffer));
        }
    }
    
    private readonly Dictionary<Socket, HashSet<int>> _receivedChunks = new();
    
    // Placeholder for more sophisticated chunk-tracking logic
    private bool AllChunksReceived(Socket socket, int totalChunks, int chunkIndex)
    {
        if (!_receivedChunks.ContainsKey(socket))
        {
            _receivedChunks[socket] = new HashSet<int>();
        }
    
        _receivedChunks[socket].Add(chunkIndex);

        return _receivedChunks[socket].Count == totalChunks;
    }
    
    // create method for handling NetworkMessage
    private void HandleMessage(Socket connection, NetworkMessage message)
    {
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
                            Stop();
                            return;
                        default:
                            Debug.Warn("transport message not handled: " + transportMessage.Code);
                            break;
                    }
                    break;
                case MessageType.MIRROR:
                    Send(connection, message);
                    break;
                case MessageType.PING:
                    Send(new PongMessage());
                    break;
                case MessageType.PONG:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
                    
            RaiseEvent(OnMessageReceived, message);
        }
    }
    
    protected void Send(Socket connection, NetworkMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        
        if(serializedMessage.Length > MaxBufferSize)
        {
            // Chunk the message
            int chunkCount = CalculateChunkCount(serializedMessage.Length);
            for(int i = 0; i < chunkCount; i++)
            {
                // Create and send chunk
                var chunk = CreateChunkWithFooter(serializedMessage, i, chunkCount);
                Console.WriteLine($"sending message chunk of size {chunk.Length}/{MaxBufferSize} [{message.Type}]");
                Send(connection, chunk);
            }
        }
        else
        {
            Console.WriteLine($"sending message of size {serializedMessage.Length}/{MaxBufferSize} [{message.Type}]");
            Send(connection, serializedMessage);
        }
    }
    
    private int CalculateChunkCount(int messageSize)
    {
        int headerSize = 8; // 4 bytes for chunkIndex and 4 bytes for totalChunks
        int maxDataSize = MaxBufferSize - headerSize;

        return (int)Math.Ceiling((double)messageSize / maxDataSize);
    }
    
    private byte[] CreateChunkWithFooter(byte[] serializedMessage, int chunkIndex, int totalChunks)
    {
        int footerSize = 8; // 4 bytes for chunkIndex and 4 bytes for totalChunks
        int maxDataSize = MaxBufferSize - footerSize;

        int offset = chunkIndex * maxDataSize;
        int length = Math.Min(maxDataSize, serializedMessage.Length - offset);

        // Create a chunk footer: 4 bytes for chunkIndex and 4 bytes for totalChunks
        byte[] footer = new byte[footerSize];
        BitConverter.GetBytes(chunkIndex).CopyTo(footer, 0);
        BitConverter.GetBytes(totalChunks).CopyTo(footer, 4);

        // Create the chunk with the footer at the end
        byte[] chunk = new byte[MaxBufferSize]; // Initialize to MaxBufferSize
        Array.Copy(serializedMessage, offset, chunk, 0, length);
        Array.Copy(footer, 0, chunk, MaxBufferSize - footerSize, footerSize);

        return chunk;
    }

    
    private void Send(Socket connection, byte[] buffer)
    {
        connection.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, OnSendCallback, connection);
    }
    
    public virtual void Send(NetworkMessage message)
    {
        Send(Socket, message);
    }
    
    private void OnSendCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState;
        socket?.EndSend(ar);
    }
    
    public virtual void Stop()
    {
        if (IsClosing)
        {
            Debug.Log("client is already closing");
            return;
        }
    
        if (!IsRunning)
        {
            Debug.Log("client is not running");
            return;
        }
    
        IsClosing = true;
    
        // Clear state
        _incompleteMessages.Clear();
        _receivedChunks.Clear();
        
        try
        {
            // Shutdown the socket before closing it
            Socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., the socket is already closed)
            Debug.Log("Exception during socket shutdown: " + ex.Message);
        }
    
        Socket.Close();
        Socket = null;
    
        IsClosing = false;
    }
    
    // Safely invoke an event
    protected void RaiseEvent<T>(Delegate<T> eventToRaise, T arg)
    {
        Delegate<T> handler = eventToRaise;
        handler?.Invoke(arg);
    }
}