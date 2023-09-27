using nez.net.test.components;
using NUnit.Framework;

namespace nez.net.test.weaving;

[TestFixture]
public class CommandWeaving
{
    [Test, Timeout(1000)]
    public void CommandReroutingTest()
    {
        TestNetworkComponent testNetworkComponent = new();
        testNetworkComponent.RequestTestCommand();
    }
}