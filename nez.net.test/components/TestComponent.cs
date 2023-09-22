using Nez;
using nez.net.components;

namespace nez.net.test;

public class TestComponent : NetworkComponent
{
    
    [Command]
    public void TestCommand()
    {
        Console.WriteLine("TestCommand");
    }
}