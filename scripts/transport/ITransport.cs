namespace nez.net.transport;

public interface ITransport
{
    void Send(byte[] data, int channelId);
    bool TryReceive(out byte[] data, out int channelId);
    void Connect(string address);
    void Disconnect();
    bool IsConnected();
}
