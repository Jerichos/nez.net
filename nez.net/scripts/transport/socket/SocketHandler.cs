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
    
    public Delegate<NetworkMessage> OnReceive { get; set; }
    public Delegate<TransportCode> OnTransportMessage { get; set; }
    
    private readonly Dictionary<Socket, List<byte>> _incompleteMessages = new();
    
    protected Socket Socket;

    protected void HandleReceive(IAsyncResult ar)
    {
        if(!IsRunning || IsClosing)
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

            int chunkIndex = BitConverter.ToInt32(buffer, 0);
            int totalChunks = BitConverter.ToInt32(buffer, 4);
            
            // Append this chunk to the buffer
            _incompleteMessages[senderSocket].AddRange(new ArraySegment<byte>(buffer, 8, receivedLength - 8));

            if (AllChunksReceived(senderSocket, totalChunks))
            {
                try
                {
                    _incompleteMessages[senderSocket].AddRange(new ArraySegment<byte>(buffer, 8, receivedLength - 8));
                    byte[] actualMessage = _incompleteMessages[senderSocket].ToArray();
                    NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
                    HandleMessage(message);
                    _incompleteMessages[senderSocket].Clear();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Message is probably incomplete, waiting for more data...");
                }
            }
            
            // Start another receive operation
            senderSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, HandleReceive, state);
        }
    }
    
    // Placeholder for more sophisticated chunk-tracking logic
    private bool AllChunksReceived(Socket socket, int totalChunks)
    {
        // For now, we simply check if the received byte count matches or exceeds
        // the expected total byte count based on totalChunks and MaxBufferSize
        return _incompleteMessages[socket].Count >= (totalChunks * MaxBufferSize);
    }
    
    // create method for handling NetworkMessage
    private void HandleMessage(NetworkMessage message)
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
                    break;
                case MessageType.PING:
                    break;
                case MessageType.PONG:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
                    
            RaiseEvent(OnReceive, message);
        }
    }
    
    protected void Send(Socket connection, NetworkMessage message)
    {
        if (message is TransportMessage transportMessage)
        {
            Console.WriteLine("transport message: " + transportMessage.Code);
        }
        
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        
        if(serializedMessage.Length > MaxBufferSize)
        {
            // Chunk the message
            int chunkCount = CalculateChunkCount(serializedMessage.Length);
            for(int i = 0; i < chunkCount; i++)
            {
                // Create and send chunk
                var chunk = CreateChunkWithHeader(serializedMessage, i, chunkCount);
                Console.WriteLine($"sending message chunk of size {chunk.Length}/{MaxBufferSize} [{message.Type}]");
                SendChunk(connection, chunk);
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
        return (int)Math.Ceiling((double)messageSize / MaxBufferSize);
    }
    
    private byte[] CreateChunk(byte[] serializedMessage, int chunkIndex, int totalChunks)
    {
        int offset = chunkIndex * MaxBufferSize;
        int length = Math.Min(MaxBufferSize, serializedMessage.Length - offset);
    
        byte[] chunk = new byte[length];
        Array.Copy(serializedMessage, offset, chunk, 0, length);
    
        return chunk;
    }
    
    private byte[] CreateChunkWithHeader(byte[] serializedMessage, int chunkIndex, int totalChunks)
    {
        int offset = chunkIndex * MaxBufferSize;
        int length = Math.Min(MaxBufferSize, serializedMessage.Length - offset);
    
        // Create a chunk header: 4 bytes for chunkIndex and 4 bytes for totalChunks
        byte[] header = new byte[8];
        BitConverter.GetBytes(chunkIndex).CopyTo(header, 0);
        BitConverter.GetBytes(totalChunks).CopyTo(header, 4);
    
        // Create the chunk with the header
        byte[] chunk = new byte[length + header.Length];
        Array.Copy(header, 0, chunk, 0, header.Length);
        Array.Copy(serializedMessage, offset, chunk, header.Length, length);
    
        return chunk;
    }
    
    private void SendChunk(Socket connection, byte[] chunk)
    {
        connection.BeginSend(chunk, 0, chunk.Length, SocketFlags.None, OnSendCallback, connection);
    }
    
    private void Send(Socket connection, byte[] message)
    {
        connection.BeginSend(message, 0, message.Length, SocketFlags.None, OnSendCallback, connection);
    }
    
    public virtual void Send(NetworkMessage message)
    {
        byte[] serializedMessage = ZeroFormatterSerializer.Serialize(message);
        Socket.BeginSend(serializedMessage, 0, serializedMessage.Length, SocketFlags.None, OnSendCallback, Socket);
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
        Socket.Close();
        Socket = null;
    }
    
    // Safely invoke an event
    protected void RaiseEvent<T>(Delegate<T> eventToRaise, T arg)
    {
        Delegate<T> handler = eventToRaise;
        handler?.Invoke(arg);
    }
}