using nez.net.components;
using nez.net.transport.socket;

namespace nez.net.test.components;

public class TestNetworkComponent : NetworkComponent
{
    public TestNetworkComponent() { }
    public TestNetworkComponent(ISocketServerHandler serverHandler) : base(serverHandler) { }

    [Command]
    public void RequestTestCommand()
    {
        Console.WriteLine("1");
    }

    [Command]
    public void RequestCommand()
    {
        Console.WriteLine("11");
    }
}