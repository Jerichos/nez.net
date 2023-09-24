using System;
using System.Collections.Generic;
using Nez;
using nez.net.transport.socket;

namespace nez.net.components;

public abstract class NetworkComponent : Nez.Component
{
    public Guid ComponentID { get; internal set; } // Unique ID to identify this component over the network
    public Guid IdentityID { get; internal set; } // Unique ID to identify to which entity this component belongs
    
    public NetworkIdentity NetworkIdentity { get; internal set; }

    private ISocketServerHandler _serverHandler;
    
    public NetworkComponent() { }
    public NetworkComponent(ISocketServerHandler serverHandler)
    {
        _serverHandler = serverHandler;
    }

    private void RegisterComponent(ISocketServerHandler serverHandler)
    {
        serverHandler.NetworkState.RegisterNetworkComponent(this);
    }

    private void UnregisterComponent(ISocketServerHandler serverHandler)
    {
        serverHandler.NetworkState.RegisterNetworkComponent(this);
    }

    internal void HandleSyncAttributeInternal()
    {
        
    }

    public override void OnAddedToEntity()
    {
        // remove component if entity does not have NetworkIdentity
        if (!Entity.HasComponent<NetworkIdentity>())
        {
            // Log a message or throw an exception to make debugging easier
            Nez.Debug.Log("Entity does not have a NetworkIdentity component. Removing the new one.");
            Entity.RemoveComponent(this);
        }
        
        // register entity
        // TODO: add easier way to get serverHandler
        RegisterComponent(_serverHandler);
    }
}