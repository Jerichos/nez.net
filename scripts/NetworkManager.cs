using nez.net.transport;

namespace nez.net;

public class NetworkManager
{
    private ITransport _transport;
    
    public NetworkManager(ITransport transport)
    {
        _transport = transport;
    }
    
    public void SendPacket(byte[] data, int channelId)
    {
        _transport.Send(data, channelId);
    }
    
    public bool TryReceivePacket(out byte[] data, out int channelId)
    {
        return _transport.TryReceive(out data, out channelId);
    }
    
    // TODO: Add more methods here
}