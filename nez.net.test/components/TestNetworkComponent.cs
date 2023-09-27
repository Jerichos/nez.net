using nez.net.components;
using nez.net.transport.socket;

namespace nez.net.test.components;

public class TestNetworkComponent : NetworkComponent
{
    // TODO: IL weave this
    public TestNetworkComponent() { }
    public TestNetworkComponent(ISocketServerHandler serverHandler) : base(serverHandler) { }

    [Command]
    public void RequestTestCommand()
    {
        Console.WriteLine("Test command");
    }
}