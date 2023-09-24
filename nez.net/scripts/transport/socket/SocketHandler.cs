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
        Socket senderSocket = state.Item1;
        byte[] buffer = state.Item2;
        
        int receivedLength = senderSocket.EndReceive(ar);
        
        // print buffer contents to console
        Console.WriteLine("buffer contents after EndReceive: " + BitConverter.ToString(buffer));

        if (receivedLength > 0)
        {
            int totalChunks = BitConverter.ToInt32(buffer, 4);

            if (totalChunks == 0)
            {
                var actualMessage = new ArraySegment<byte>(buffer, 1, receivedLength - 1).ToArray();
                NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
                HandleMessage(message);
            }
            else
            {
                int chunkIndex = BitConverter.ToInt32(buffer, 0);
                
                if (!_incompleteMessages.ContainsKey(senderSocket))
                {
                    _incompleteMessages[senderSocket] = new List<byte>();
                }
                
                // Append this chunk to the buffer
                _incompleteMessages[senderSocket].AddRange(new ArraySegment<byte>(buffer, 8, receivedLength - 8));
                if (AllChunksReceived(senderSocket, totalChunks, chunkIndex))
                {
                    try
                    {
                        byte[] actualMessage = _incompleteMessages[senderSocket].ToArray();
                        // Deserialize the actual message
                        NetworkMessage message = ZeroFormatterSerializer.Deserialize<NetworkMessage>(actualMessage);
            
                        // Handle the message and clear the buffer
                        HandleMessage(message);
                        _incompleteMessages[senderSocket].Clear();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        Console.WriteLine("Message is probably incomplete, waiting for more data...");
                    }
                }
            }
            
            // Start another receive operation
            byte[] newBuffer = new byte[MaxBufferSize];
            senderSocket.BeginReceive(newBuffer, 0, newBuffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(senderSocket, newBuffer));
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
                // print to console buffer content
                Console.WriteLine("chunk buffer contents before BeginSend [chunkIndex: "+ i +"]: " + BitConverter.ToString(chunk));
                Send(connection, chunk);
            }
        }
        else
        {
            Console.WriteLine($"sending message of size {serializedMessage.Length}/{MaxBufferSize} [{message.Type}]");
            // print to console buffer content
            Console.WriteLine("no-chunk buffer contents before BeginSend: " + BitConverter.ToString(serializedMessage));
            Send(connection, serializedMessage);
        }
    }
    
    private int CalculateChunkCount(int messageSize)
    {
        int headerSize = 8; // 4 bytes for chunkIndex and 4 bytes for totalChunks
        int maxDataSize = MaxBufferSize - headerSize;

        return (int)Math.Ceiling((double)messageSize / maxDataSize);
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
        int headerSize = 8; // 4 bytes for chunkIndex and 4 bytes for totalChunks
        int maxDataSize = MaxBufferSize - headerSize;

        int offset = chunkIndex * maxDataSize;
        int length = Math.Min(maxDataSize, serializedMessage.Length - offset);

        // Create a chunk header: 4 bytes for chunkIndex and 4 bytes for totalChunks
        byte[] header = new byte[headerSize];
        BitConverter.GetBytes(chunkIndex).CopyTo(header, 0);
        BitConverter.GetBytes(totalChunks).CopyTo(header, 4);

        // Create the chunk with the header
        byte[] chunk = new byte[length + header.Length];
        Array.Copy(header, 0, chunk, 0, header.Length);
        Array.Copy(serializedMessage, offset, chunk, header.Length, length);

        return chunk;
    }
    
    private void Send(Socket connection, byte[] buffer)
    {
        connection.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, OnSendCallback, connection);
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