using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using nez.net.components;

namespace nez.net
{
public class NetworkState
{
    private static NetworkState _instance;

    public static NetworkState Instance
    {
        get
        {
            return _instance ??= new NetworkState();
        }
    }

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
        var newGuid = Guid.NewGuid();
        networkComponent.NetworkID = newGuid;
        
        if (!_networkComponents.TryAdd(networkComponent.NetworkID, networkComponent))
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

    public void SetNetworkState(Dictionary<Guid, NetworkIdentity> networkEntities, Dictionary<Guid, NetworkComponent> networkComponents)
    {
        ConcurrentDictionary<Guid, NetworkIdentity> concurrentNetworkEntities = new ConcurrentDictionary<Guid, NetworkIdentity>(networkEntities);
        ConcurrentDictionary<Guid, NetworkComponent> concurrentNetworkComponents = new ConcurrentDictionary<Guid, NetworkComponent>(networkComponents);

        SetNetworkState(concurrentNetworkEntities, concurrentNetworkComponents);
    }
}
}