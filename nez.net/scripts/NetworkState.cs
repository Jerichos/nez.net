using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nez;
using nez.net.components;

namespace nez.net
{
public class NetworkState
{
    private readonly ConcurrentDictionary<Guid, NetworkIdentity> _networkEntities = new();
    private readonly ConcurrentDictionary<Guid, NetworkComponent> _networkComponents = new();
        
    internal void RegisterNetworkEntity(NetworkIdentity networkIdentity)
    {
        var newGuid = Guid.NewGuid();
        networkIdentity.NetworkID = newGuid;
        
        if (!_networkEntities.TryAdd(networkIdentity.NetworkID, networkIdentity))
        {
            // Handle error: duplicate ID
            throw new System.Exception("NetworkID already registered");
        }
    }
        
    internal void RegisterNetworkComponent(NetworkComponent networkComponent)
    {
        networkComponent.ComponentID = Guid.NewGuid();
        networkComponent.IdentityID = networkComponent.NetworkIdentity.NetworkID;
        
        if (!_networkComponents.TryAdd(networkComponent.ComponentID, networkComponent))
        {
            // Handle error: duplicate ID
            throw new System.Exception("NetworkID already registered");
        }
    }

    internal void UnregisterNetworkEntity(Guid networkID)
    {
        _networkEntities.TryRemove(networkID, out _);
    }
        
    internal void UnregisterNetworkComponent(Guid networkID)
    {
        _networkComponents.TryRemove(networkID, out _);
    }

    internal NetworkIdentity GetNetworkEntity(Guid networkID)
    {
        _networkEntities.TryGetValue(networkID, out NetworkIdentity entity);
        return entity;
    }

    internal NetworkComponent GetNetworkComponent(Guid networkID)
    {
        _networkComponents.TryGetValue(networkID, out NetworkComponent component);
        return component;
    }
    
    internal ConcurrentDictionary<Guid, NetworkIdentity> GetNetworkEntities()
    {
        return _networkEntities;
    }

    internal ConcurrentDictionary<Guid, NetworkComponent> GetNetworkComponents()
    {
        return _networkComponents;
    }

    public void SetNetworkState(ConcurrentDictionary<Guid, NetworkIdentity> networkEntities, ConcurrentDictionary<Guid, NetworkComponent> networkComponents)
    {
        if (networkEntities != null)
        {
            // Create or update entities based on received state
            foreach (var networkEntityPair in networkEntities)
            {
                NetworkIdentity receivedNetworkIdentity = networkEntityPair.Value;
                
                if (_networkEntities.TryGetValue(networkEntityPair.Key, out NetworkIdentity existingNetworkIdentity))
                {
                    // Update existing entity
                    // Example: existingNetworkIdentity.Position = receivedNetworkIdentity.Position;
                }
                else
                {
                    // Create new entity
                    // Example: CreateEntity(receivedNetworkIdentity);
                    Entity entity = new Entity();
                    entity.AddComponent(receivedNetworkIdentity);
                    
                    // TODO: add entity to a scene
                    
                    _networkEntities.TryAdd(networkEntityPair.Key, receivedNetworkIdentity);
                }
            }
            
            // Remove stale entities that are not part of the new network state
            foreach (var existingEntityPair in _networkEntities)
            {
                if (!networkEntities.ContainsKey(existingEntityPair.Key))
                {
                    // Remove entity from game
                    // Example: RemoveEntity(existingEntityPair.Value);
                    _networkEntities.TryRemove(existingEntityPair.Key, out _);
                }
            }
        }

        if (networkComponents != null)
        {
            // Update components based on received state
            foreach (var networkComponentPair in networkComponents)
            {
                NetworkComponent receivedNetworkComponent = networkComponentPair.Value;

                if (_networkComponents.TryGetValue(networkComponentPair.Key, out NetworkComponent existingNetworkComponent))
                {
                    // Update existing component
                    // Example: existingNetworkComponent.SomeAttribute = receivedNetworkComponent.SomeAttribute;
                }
                else
                {
                    // Create new component
                    // Example: CreateComponent(receivedNetworkComponent);
                    
                    // get matching network indentity
                    NetworkIdentity networkIdentity = GetNetworkEntity(receivedNetworkComponent.IdentityID);
                    // networkIdentity.Entity.AddComponent(receivedNetworkComponent);
                    
                    _networkComponents.TryAdd(networkComponentPair.Key, receivedNetworkComponent);
                }
            }
            
            // Remove stale components that are not part of the new network state
            foreach (var existingComponentPair in _networkComponents)
            {
                if (!networkComponents.ContainsKey(existingComponentPair.Key))
                {
                    // Remove component
                    // Example: RemoveComponent(existingComponentPair.Value);
                    _networkComponents.TryRemove(existingComponentPair.Key, out _);
                }
            }
        }
    }

    public void SetNetworkState(Dictionary<Guid, NetworkIdentity> networkEntities, Dictionary<Guid, NetworkComponent> networkComponents)
    {
        ConcurrentDictionary<Guid, NetworkIdentity> concurrentNetworkEntities = null;
        ConcurrentDictionary<Guid, NetworkComponent> concurrentNetworkComponents = null;
            
            if(networkEntities != null)
                concurrentNetworkEntities = new ConcurrentDictionary<Guid, NetworkIdentity>(networkEntities);
            if(networkComponents != null)
                concurrentNetworkComponents = new ConcurrentDictionary<Guid, NetworkComponent>(networkComponents);
            
        SetNetworkState(concurrentNetworkEntities, concurrentNetworkComponents);
    }

    public void UpdateScene(Scene clientScene)
    {
        // iterate thru network identities and add entities to the scene
        foreach (var networkEntityPair in _networkEntities)
        {
            NetworkIdentity networkIdentity = networkEntityPair.Value;
            clientScene.AddEntity(networkIdentity.Entity);
        }
    }
}
}