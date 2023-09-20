namespace nez.net.transport.socket;

public class SocketTransport : ITransport
{
    public void Send(byte[] data, int channelId)
    {
        throw new System.NotImplementedException();
    }

    public bool TryReceive(out byte[] data, out int channelId)
    {
        throw new System.NotImplementedException();
    }

    public void Connect(string address)
    {
        throw new System.NotImplementedException();
    }

    public void Disconnect()
    {
        throw new System.NotImplementedException();
    }

    public bool IsConnected()
    {
        throw new System.NotImplementedException();
    }
}