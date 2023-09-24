using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nez.net.components;
using ZeroFormatter;
using ZeroFormatter.Formatters;
using ZeroFormatter.Internal;

namespace nez.net.transport.socket
{
public class NetworkStateFormatter<TTypeResolver> : Formatter<TTypeResolver, Dictionary<Guid, NetworkIdentity>> 
    where TTypeResolver : ITypeResolver, new()
{
    private readonly Formatter<TTypeResolver, KeyValuePair<Guid, NetworkIdentity>> formatter;
    
    public NetworkStateFormatter()
    {
        this.formatter = Formatter<TTypeResolver, KeyValuePair<Guid, NetworkIdentity>>.Default;
    }

    public override int? GetLength()
    {
        return null; // Size is variable
    }

    public override int Serialize(ref byte[] bytes, int offset, Dictionary<Guid, NetworkIdentity> value)
    {
        int startOffset = offset;

        // Write the number of elements in the dictionary
        offset += BinaryUtil.WriteInt32(ref bytes, offset, value.Count);

        foreach (var kvp in value)
        {
            offset += formatter.Serialize(ref bytes, offset, kvp);
        }

        return offset - startOffset;
    }

    public override Dictionary<Guid, NetworkIdentity> Deserialize(ref byte[] bytes, int offset, DirtyTracker tracker, out int byteSize)
    {
        int startOffset = offset;

        int count = BinaryUtil.ReadInt32(ref bytes, offset);
        offset += 4; // 4 bytes for int

        var dict = new Dictionary<Guid, NetworkIdentity>();

        for (int i = 0; i < count; i++)
        {
            int size;
            var kvp = formatter.Deserialize(ref bytes, offset, tracker, out size);
            offset += size;

            dict.TryAdd(kvp.Key, kvp.Value);
        }

        byteSize = offset - startOffset;
        return dict;
    }
}

public class NetworkIdentityFormatter<TTypeResolver> : Formatter<TTypeResolver, NetworkIdentity>
    where TTypeResolver : ITypeResolver, new()
{
    public override int? GetLength()
    {
        return null; // Variable size
    }

    public override int Serialize(ref byte[] bytes, int offset, NetworkIdentity value)
    {
        int startOffset = offset;
        offset += BinaryUtil.WriteGuid(ref bytes, offset, value.NetworkID);

        return offset - startOffset;
    }

    public override NetworkIdentity Deserialize(ref byte[] bytes, int offset, DirtyTracker tracker, out int byteSize)
    {
        int startOffset = offset;
        Guid networkID = BinaryUtil.ReadGuid(ref bytes, offset);
        offset += 16; // 16 bytes for Guid

        byteSize = offset - startOffset;

        return new NetworkIdentity { NetworkID = networkID};
    }
}

public class NetworkComponentFormatter<TTypeResolver> : Formatter<TTypeResolver, NetworkComponent>
    where TTypeResolver : ITypeResolver, new()
{
    public override int? GetLength()
    {
        return null; // Variable size
    }

    public override int Serialize(ref byte[] bytes, int offset, NetworkComponent value)
    {
        int startOffset = offset;

        // Write type information
        string type = value.GetType().AssemblyQualifiedName;
        offset += Formatter<TTypeResolver, string>.Default.Serialize(ref bytes, offset, type);

        // Write the common fields
        offset += BinaryUtil.WriteGuid(ref bytes, offset, value.ComponentID);
        
        // Write the common fields
        offset += BinaryUtil.WriteGuid(ref bytes, offset, value.IdentityID);

        // Additional serialization for derived types can go here
        // ...

        return offset - startOffset;
    }

    public override NetworkComponent Deserialize(ref byte[] bytes, int offset, DirtyTracker tracker, out int byteSize)
    {
        int startOffset = offset;

        // Read type information
        int size;
        string type = Formatter<TTypeResolver, string>.Default.Deserialize(ref bytes, offset, tracker, out size);
        offset += size;

        // Create the correct type
        NetworkComponent component = (NetworkComponent)Activator.CreateInstance(Type.GetType(type));

        // Read the common fields
        component.ComponentID = BinaryUtil.ReadGuid(ref bytes, offset);
        offset += 16; // 16 bytes for Guid
        component.IdentityID = BinaryUtil.ReadGuid(ref bytes, offset);
        offset += 16; // 16 bytes for Guid

        // Additional deserialization for derived types can go here
        // ...

        byteSize = offset - startOffset;
        return component;
    }
}
}