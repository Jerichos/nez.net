using System;
using Nez;
using nez.net.transport.socket;

namespace nez.net.components;

public sealed class NetworkIdentity : Component
{
    public Guid NetworkID { get; internal set; } // Unique ID to identify this entity over the network
    
    public bool IsLocalPlayer { get; internal set; } // True if this entity is the local player
    public bool IsServer;
    public bool IsClient;
    public bool HasAuthority;
    
    private readonly ISocketServerHandler _serverHandler;

    public NetworkIdentity(ISocketServerHandler serverHandler)
    {
        _serverHandler = serverHandler;
    }

    public NetworkIdentity()
    {
    }

    private void RegisterIdentity()
    {
        _serverHandler.NetworkState.RegisterNetworkEntity(this);
    }
    
    private void UnregisterIdentity()
    {
        _serverHandler.NetworkState.UnregisterNetworkEntity(NetworkID);
    }

    public override void OnAddedToEntity()
    {
        // check if there are multiple NetworkIdentity components, if there is more than one, remove the new one
        if(Entity.GetComponents<NetworkIdentity>().Count > 1)
        {
            // Log a message or throw an exception to make debugging easier
            Nez.Debug.Log("Entity already has a NetworkIdentity component. Removing the new one.");
            Entity.RemoveComponent(this);
            return;
        }
        
        // register
        RegisterIdentity();
    }
}