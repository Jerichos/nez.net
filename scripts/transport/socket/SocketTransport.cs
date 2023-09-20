using System;
using System.Net.Sockets;
using Nez;

namespace nez.net.transport.socket;

public class SocketTransport : ITransport
{
    private TcpClient _client;
    private NetworkStream _stream;
    private bool _isConnected;
    
    public void Connect(string address)
    {
        try
        {
            _client = new TcpClient(address, 7777);
            _stream = _client.GetStream();
            _isConnected = true;
        }
        catch (Exception e)
        {
            Debug.Error($"Could not connect to server {e.Message}");
        }
    }
    
    public void Disconnect()
    {
        _stream.Close();
        _client.Close();
        _isConnected = false;
    }
    
    public bool IsConnected()
    {
        return _isConnected;
    }
    
    public void Send(byte[] data, int channelId)
    {
        if(_isConnected)
        {
            _stream.Write(data, 0, data.Length);
        }
    }

    public bool TryReceive(out byte[] data, out int channelId)
    {
        data = null;
        channelId = 0;
        
        if(!_isConnected)
        {
            return false;
        }
        
        if(_stream.DataAvailable)
        {
            data = new byte[1024]; // max size of packet?
            int bytesRead = _stream.Read(data, 0, data.Length);
            Array.Resize(ref data, bytesRead);
            return true;
        }

        return false;
    }
}