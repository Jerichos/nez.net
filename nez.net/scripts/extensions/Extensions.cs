using Nez;
using nez.net.components;

namespace nez.net.extensions;

public static class Extensions
{
    public static T AddComponentUnique<T>(this Entity entity) where T : Component, new()
    {
        var component = entity.GetComponent<T>();

        if (component == null)
        {
            component = entity.AddComponent<T>();
        }

        return component;
    }
    
    public static T AddComponentUnique<T>(this Entity entity, T component) where T : Component
    {
        var existingComponent = entity.GetComponent<T>();

        if (existingComponent == null)
        {
            entity.AddComponent(component);
        }

        return component;
    }
    
    // add NetworkComponent T to entity, only if it has NetworkIdentity
    public static T AddNetworkComponent<T>(this Entity entity, T component) where T : NetworkComponent, new()
    {
        var networkIdentity = entity.GetComponent<NetworkIdentity>();

        if (networkIdentity == null)
        {
            throw new System.Exception("Entity does not have NetworkIdentity");
        }

        // entity.AddComponent(component);
        component.NetworkIdentity = networkIdentity;
        return component;
    }
}