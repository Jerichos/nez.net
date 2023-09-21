using nez.net.transport;

namespace nez.net;

public class NetworkManager
{
    private ITransport _transport;
    
    public NetworkManager(ITransport transport)
    {
        _transport = transport;
    }

    public void Initialize()
    {
        // Initialize your networking here
        // For testing, you can use localhost
        _transport.Connect("127.0.0.1");
    }

    public void Update()
    {
        // This method will be called every frame to handle networking

        // Example of sending some data
        byte[] dataToSend = new byte[] { 1, 2, 3, 4 };
        _transport.Send(dataToSend, 0);

        // Example of receiving some data
        if (_transport.TryReceive(out byte[] receivedData, out int channelId))
        {
            // Process the received data here
        }
    }
    
    // TODO: Add more methods here
}
