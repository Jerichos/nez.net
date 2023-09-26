using nez.net.components;
using nez.net.transport;
using nez.net.transport.socket;
using ZeroFormatter;

namespace nez.net.test.zeroFormatter;

[TestFixture]
public class FormatterTests
{
    [Test]
    public void TransportMessageTest()
    {
        TestMessage(new TransportMessage(){Code = TransportCode.CLIENT_ERROR});
    }

    [Test]
    public void PingMessageTest()
    {
        TestMessage(new PingMessage());
    }
    
    [Test]
    public void PongMessageTest()
    {
        TestMessage(new PongMessage());
    }
    
    [Test]
    public void NetworkStateMessageTest()
    {
        TestMessage(new NetworkStateMessage(){NetworkComponents = new Dictionary<Guid, NetworkComponent>(), NetworkEntities = new Dictionary<Guid, NetworkIdentity>()});
    }
    
    private void TestMessage(NetworkMessage message)
    {
        var serializedMessage = ZeroFormatterSerializer.Serialize(message);
        var deserializedMessage = ZeroFormatterSerializer.Deserialize<NetworkMessage>(serializedMessage);
        Assert.AreEqual(message.Type, deserializedMessage.Type);
    }
}