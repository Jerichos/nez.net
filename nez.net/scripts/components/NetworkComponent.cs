using System;
using System.Collections.Generic;
using Nez;

namespace nez.net.components;

public abstract class NetworkComponent : Nez.Component
{
    public Guid NetworkID { get; internal set; } // Unique ID to identify this component over the network
    public NetworkIdentity NetworkIdentity { get; internal set; }

    internal void RegisterComponent()
    {
        NetworkState.Instance.RegisterNetworkComponent(this);
    }

    internal void UnregisterComponent()
    {
        NetworkState.Instance.UnregisterNetworkComponent(NetworkID);
    }

    public override void OnAddedToEntity()
    {
        NetworkIdentity = Entity.GetOrCreateComponent<NetworkIdentity>();
    }
}