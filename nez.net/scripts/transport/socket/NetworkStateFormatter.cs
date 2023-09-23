using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
}