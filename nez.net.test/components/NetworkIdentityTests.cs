using Nez;
using nez.net.components;
using nez.net.extensions;
using NUnit.Framework;

namespace nez.net.test.components;

[TestFixture]
public class NetworkIdentityTests
{
    [Test]
    public void ShouldOnlyAllowOneNetworkIdentityPerEntity()
    {
        // Arrange
        var entity = new Entity("TestEntity");
        
        // Act
        var networkIdentity1 = new NetworkIdentity();
        var networkIdentity2 = new NetworkIdentity();
        
        entity.AddComponentUnique(networkIdentity1);
        
        // Assert
        Assert.IsTrue(entity.HasComponent<NetworkIdentity>());
        
        // Act
        entity.AddComponentUnique(networkIdentity2);
        
        // Assert that only one NetworkIdentity exists
        var components = entity.GetComponents<NetworkIdentity>();
        
        Assert.AreEqual(1, components.Count);
        
        // Optionally, you can assert that it's the first NetworkIdentity that remains
        Assert.AreSame(networkIdentity1, entity.GetComponent<NetworkIdentity>());
    }
}