using System;
using Nez;

namespace nez.net.components;

public class NetworkIdentity : Component
{
    public Guid NetworkID { get; internal set; } // Unique ID to identify this entity over the network
    
    public bool IsLocalPlayer { get; internal set; } // True if this entity is the local player
    public bool IsServer;
    public bool IsClient;
    public bool HasAuthority;
    
    internal void RegisterIdentity()
    {
        NetworkState.Instance.RegisterNetworkEntity(this);
    }
    
    internal void UnregisterIdentity()
    {
        NetworkState.Instance.UnregisterNetworkEntity(NetworkID);
    }

    public override void OnAddedToEntity()
    {
        if (Entity.HasComponent<NetworkIdentity>())
        {
            // Log a message or throw an exception to make debugging easier
            Nez.Debug.Log("Entity already has a NetworkIdentity component. Removing the new one.");
            Entity.RemoveComponent(this);
        }
    }
}