using Nez;

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
}