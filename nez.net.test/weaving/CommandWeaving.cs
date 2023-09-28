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
        testNetworkComponent.RequestCommand();
        
        TestWeavedNewMethod("RequestTestCommand");
        TestWeavedNewMethod("RequestCommand");
    }

    private void TestWeavedNewMethod(string commandMethodName)
    {
        var type = typeof(TestNetworkComponent); // Replace with the actual class name
        Assert.IsNotNull(type.GetMethod("Invoke" + commandMethodName)); // Replace with the actual method name

        var instance = new TestNetworkComponent(); // Replace with the actual class name and constructor parameters if any
        var method = type.GetMethod("Invoke" + commandMethodName); // Replace with the actual method name
        var result = method.Invoke(instance, new object[] { /* parameters */ });
    }
}