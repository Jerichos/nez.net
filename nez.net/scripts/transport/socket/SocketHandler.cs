using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Nez;
using ZeroFormatter;

namespace nez.net.transport.socket;

public delegate void Delegate<in T>(params T[] args);
public delegate void Delegate<in T1, in T2>(T1 arg1, T2 arg2);

public abstract class SocketHandler : ISocketHandler
{
    public virtual bool IsRunning => Socket != null && Socket.Connected;
    public int MaxBufferSize { get; set; }
    
    protected bool IsClosing;
    
    public NetworkState NetworkState { get; set; }
    
    public Delegate<Socket, NetworkMessage> OnMessageReceived { get; set; }
    public Delegate<TransportCode> OnTransportMessage { get; set; }
    
    public Socket Socket { get; set; }

    // performance
    public long TotalBitsSent { get; private set; } = 0;
    public long TotalBitsReceived { get; private set; } = 0;

    public double SendBitRate => _sendBitRate;          // TODO: this is not updated
    public double ReceiveBitRate => _receiveBitRate;    // TODO: this is not updated
    
    private double _sendBitRate;
    private double _receiveBitRate;
    
    private long _lastTotalBitsSent = 0;
    private long _lastTotalBitsReceived = 0;
    
    private DateTime _lastUpdateTime;

    private readonly RingBuffer _ringBufferReceiver;
    private readonly RingBuffer _ringBufferSender;
    
    private int _messageCounter = 0;

    private MessageHandler _messageHandler;
    
    protected SocketHandler(int maxBufferSize, NetworkState networkState)
    {
        _messageHandler = new MessageHandler();
        
        _ringBufferReceiver = new RingBuffer(2048);
        _ringBufferSender = new RingBuffer(2048);
        
        MaxBufferSize = maxBufferSize;
        NetworkState = networkState;
    }
    
    protected virtual void OnStart()
    {
        _lastUpdateTime = DateTime.UtcNow;
    }

    protected void HandleReceive(IAsyncResult ar)
    {
        if(!IsRunning || IsClosing)
            return;
        
        var state = (Tuple<Socket, byte[]>)ar.AsyncState;
        Socket connection = state.Item1;
        byte[] buffer = state.Item2;
        
        int receivedLength = connection.EndReceive(ar);
        TotalBitsReceived += receivedLength * 8;
        
        if (receivedLength > 0)
        {
            _ringBufferReceiver.WriteBlock(new ArraySegment<byte>(buffer, 0, receivedLength).ToArray());
            var (success, payloadLength, isChunked, messageID) = _ringBufferReceiver.ReadPacketHeader();

            if (success)
            {
                if (isChunked)
                {
                    var (chunkIndex, chunkCount, payload) = _ringBufferReceiver.ReadChunkHeader(payloadLength);
                    NetworkMessage message = _messageHandler.HandleReceivedData(payload, messageID, true, chunkIndex, chunkCount);
                    RaiseEvent(OnMessageReceived, connection, message);
                }
                else
                {
                    var payload = _ringBufferReceiver.Read(payloadLength);
                    NetworkMessage message = _messageHandler.HandleReceivedData(payload, messageID, false, 0, 0);
                    RaiseEvent(OnMessageReceived, connection, message);
                }
            }
            
            // Start another receive operation
            if(IsClosing || !IsRunning)
                return;
            
            byte[] newBuffer = new byte[MaxBufferSize];
            connection.BeginReceive(newBuffer, 0, newBuffer.Length, SocketFlags.None, HandleReceive, Tuple.Create(connection, newBuffer));
        }
    }

    // TODO: check if it helps trim the end for ZeroFormatter deserialization to be faster
    
    
    public void Send(Socket connection, NetworkMessage message)
    {
        byte[] payload = ZeroFormatterSerializer.Serialize(message);
        
        Console.WriteLine($"Sending message of type {message.Type} of size {payload.Length}");

        ushort messageID = GetMessageIDInternal(connection);
        if(payload.Length > MaxBufferSize - 5) // -5 to exclude header
        {
            // Chunk the message
            byte chunkCount = CalculateChunkCount(payload.Length);
            for(byte i = 0; i < chunkCount; i++)
            {
                _ringBufferSender.AddPacketToRingBuffer(payload, true, messageID, i, chunkCount, MaxBufferSize);
                SendPacketFromRingBuffer(connection);
            }
        }
        else
        {
            _ringBufferSender.AddPacketToRingBuffer(payload, false, messageID);
            SendPacketFromRingBuffer(connection);
        }
    }

    private void SendPacketFromRingBuffer(Socket connection)
    {
        Send(connection, _ringBufferSender.ReadSendPacket());
    }

    public double GetSendBitRate()
    {
        UpdateBitRate();
        return _sendBitRate;
    }
    
    public double GetReceiveBitRate()
    {
        UpdateBitRate();
        return _receiveBitRate;
    }
    
    public void UpdateBitRate()
    {
        DateTime now = DateTime.UtcNow;
        double elapsedSeconds = (now - _lastUpdateTime).TotalSeconds;
        
        _sendBitRate = (TotalBitsSent - _lastTotalBitsSent) / elapsedSeconds;
        _receiveBitRate = (TotalBitsReceived - _lastTotalBitsReceived) / elapsedSeconds;
        
        // reset
        _lastTotalBitsSent = TotalBitsSent;
        _lastTotalBitsReceived = TotalBitsReceived;
        _lastUpdateTime = now;
    }
    
    private byte CalculateChunkCount(int messageSize)
    {
        int headerSize = 8; // 4 bytes for chunkIndex and 4 bytes for totalChunks
        int maxDataSize = MaxBufferSize - headerSize;

        return (byte)Math.Ceiling((double)messageSize / maxDataSize);
    }
    
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
        TotalBitsSent += buffer.Length * 8;
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
    
    protected void RaiseEvent<T1, T2>(Delegate<T1, T2> eventToRaise, T1 arg1, T2 arg2)
    {
        Delegate<T1, T2> handler = eventToRaise;
        handler?.Invoke(arg1, arg2);
    }
}