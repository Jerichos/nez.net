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
    
    public Socket Socket { get; set; }

    private readonly Dictionary<int, List<byte>> _incompleteMessages = new();

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
            int totalChunks = BitConverter.ToInt16(buffer, buffer.Length - 2);
            int chunkIndex = BitConverter.ToInt16(buffer, buffer.Length - 4);
            int messageID = BitConverter.ToInt16(buffer, buffer.Length - 6);

            if (totalChunks == 0)
            {
                var actualMessage = new ArraySegment<byte>(buffer, 0, receivedLength).ToArray();
                NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
                HandleMessage(connection, message);
            }
            else
            {
                Console.WriteLine($"received chunk message [{chunkIndex+1}/{totalChunks}] of size {receivedLength}/{MaxBufferSize} [{messageID}]");
                
                if (!_incompleteMessages.ContainsKey(messageID))
                {
                    _incompleteMessages[messageID] = new List<byte>();
                }
                
                // Append this chunk to the buffer
                _incompleteMessages[messageID].AddRange(new ArraySegment<byte>(buffer, 0, receivedLength - 6)); // -6 to exclude footer
                if (AllChunksReceived((ushort)messageID, totalChunks, chunkIndex))
                {
                    try
                    {
                        byte[] actualMessage = _incompleteMessages[messageID].ToArray();
                        actualMessage = TrimEnd(actualMessage);
                        Console.WriteLine($"received all chunks, message size: {actualMessage.Length} [{messageID}]");
                        // Deserialize the actual message
                        NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
            
                        // Handle the message and clear the buffer
                        HandleMessage(connection, message);
                        _incompleteMessages[messageID].Clear();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        
                        // discard current message
                        _incompleteMessages[messageID].Clear();
                        Console.WriteLine($"message was discarded: {messageID}");
                    }
                }
            }
            
            // Start another receive operation
            if(IsClosing || !IsRunning)
                return;
            
            byte[] newBuffer = new byte[MaxBufferSize];
            Console.WriteLine("BeginReceive");
            connection.BeginReceive(newBuffer, 0, newBuffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(connection, newBuffer));
        }
    }
    
    public static byte[] TrimEnd(byte[] array)
    {
        int lastIndex = Array.FindLastIndex(array, b => b != 0);

        if (lastIndex == -1)
            return Array.Empty<byte>();

        byte[] trimmedArray = new byte[lastIndex + 1];
        Array.Copy(array, trimmedArray, lastIndex + 1);
        return trimmedArray;
    }
    
    public void Send(Socket connection, NetworkMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        Console.WriteLine($"Sending message of type {message.Type} of size {serializedMessage.Length}");
        
        if(serializedMessage.Length > MaxBufferSize)
        {
            // Chunk the message
            ushort chunkCount = CalculateChunkCount(serializedMessage.Length);
            ushort messageID = GetMessageIDInternal(connection);
            for(ushort i = 0; i < chunkCount; i++)
            {
                // Create and send chunk
                var chunk = CreateChunkWithFooter(serializedMessage, i, chunkCount, messageID);
                Console.WriteLine($"sending message chunk [{i+1}/{chunkCount}] of size {chunk.Length}/{MaxBufferSize} [{message.Type}] ID: {messageID}");
                Send(connection, chunk);
            }
        }
        else
        {
            Console.WriteLine($"sending message of size {serializedMessage.Length}/{MaxBufferSize} [{message.Type}]");
            Send(connection, serializedMessage);
        }
    }
    
    private readonly Dictionary<ushort, HashSet<int>> _receivedChunks = new();
    
    // Placeholder for more sophisticated chunk-tracking logic
    private bool AllChunksReceived(ushort messageID, int totalChunks, int chunkIndex)
    {
        if (!_receivedChunks.ContainsKey(messageID))
        {
            _receivedChunks[messageID] = new HashSet<int>();
        }
    
        _receivedChunks[messageID].Add(chunkIndex);
        
        bool allChunksReceived = _receivedChunks[messageID].Count == totalChunks;
        if(allChunksReceived)
            Console.WriteLine("all chunks received");

        return allChunksReceived;
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
                    Console.WriteLine($"Set network state of SocketHandler {GetType().FullName}");
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
    
    
    private ushort CalculateChunkCount(int messageSize)
    {
        int headerSize = 8; // 4 bytes for chunkIndex and 4 bytes for totalChunks
        int maxDataSize = MaxBufferSize - headerSize;

        return (ushort)Math.Ceiling((double)messageSize / maxDataSize);
    }
    
    private byte[] CreateChunkWithFooter(byte[] serializedMessage, ushort chunkIndex, ushort totalChunks, ushort messageID)
    {
        int footerSize = 6; // 2 chunk, 2 totalChunks, 2 messageID
        int maxDataSize = MaxBufferSize - footerSize;

        int offset = chunkIndex * maxDataSize;
        int length = Math.Min(maxDataSize, serializedMessage.Length - offset);

        byte[] footer = new byte[footerSize];
        BitConverter.GetBytes(messageID).CopyTo(footer, 0);
        BitConverter.GetBytes(chunkIndex).CopyTo(footer, 2);
        BitConverter.GetBytes(totalChunks).CopyTo(footer, 4);
        
        // int messageID = BitConverter.ToInt16(buffer, buffer.Length - 2);
        // int chunkIndex = BitConverter.ToInt16(buffer, buffer.Length - 4);
        // int totalChunks = BitConverter.ToInt16(buffer, buffer.Length - 6);

        // Create the chunk with the footer at the end
        byte[] chunk = new byte[MaxBufferSize]; // Initialize to MaxBufferSize
        Array.Copy(serializedMessage, offset, chunk, 0, length);
        Array.Copy(footer, 0, chunk, MaxBufferSize - footerSize, footerSize);

        return chunk;
    }
    
    private int _messageCounter = 0;
    private ushort GetMessageIDInternal(Socket connection)
    {
        int messageID = GetConnectionID(connection) + _messageCounter++;
        if (messageID > ushort.MaxValue)
        {
            _messageCounter = 0;
            messageID = GetConnectionID(connection);
        }
    
        return (ushort)messageID;  // Cast to ushort
    }
    
    protected abstract ushort GetConnectionID(Socket connection);

    
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