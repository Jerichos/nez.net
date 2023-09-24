using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using nez.net.components;
using ZeroFormatter;

namespace nez.net.transport.socket;

public enum MessageType : byte
{
    NETWORK_STATE,
    TRANSPORT,
    MIRROR,
    PING,
    PONG,
    URI,
    SYNC,
}

[Union(typeof(TransportMessage), typeof(MirrorMessage), typeof(PingMessage), typeof(PongMessage), 
    typeof(NetworkStateMessage), typeof(SyncMessage))]
public abstract class NetworkMessage
{
    [UnionKey]
    public abstract MessageType Type { get; }
    
    private static int CalculateBufferSize<T>() where T: NetworkMessage
    {
        var instance = Activator.CreateInstance<T>();
        return ZeroFormatterSerializer.Serialize(instance).Length;
    }
    
    [IgnoreFormat]
    static Dictionary<Type, int> preCalculatedBufferSizes = new Dictionary<Type, int>();

    public static void PreCalculateBufferSizes()
    {
        preCalculatedBufferSizes[typeof(TransportMessage)] = CalculateBufferSize<TransportMessage>();
        preCalculatedBufferSizes[typeof(MirrorMessage)] = CalculateBufferSize<MirrorMessage>();
        preCalculatedBufferSizes[typeof(PingMessage)] = CalculateBufferSize<PingMessage>();
        preCalculatedBufferSizes[typeof(PongMessage)] = CalculateBufferSize<PongMessage>();
        preCalculatedBufferSizes[typeof(NetworkStateMessage)] = CalculateBufferSize<NetworkStateMessage>();
        preCalculatedBufferSizes[typeof(SyncMessage)] = CalculateBufferSize<SyncMessage>();
        // ... more types
    }

    public static int GetPreCalculatedBufferSize<T>() where T : NetworkMessage
    {
        return preCalculatedBufferSizes[typeof(T)];
    }
}

[ZeroFormattable]
public class TransportMessage : NetworkMessage
{
    public override MessageType Type => MessageType.TRANSPORT;

    [Index(0)]
    public virtual TransportCode Code { get; set; }
}

[ZeroFormattable]
public class MirrorMessage : NetworkMessage
{
    public override MessageType Type => MessageType.MIRROR;

    [Index(0)]
    public virtual string Message { get; set; }
}

[ZeroFormattable]
public class PingMessage : NetworkMessage
{
    public override MessageType Type => MessageType.PING;
}

[ZeroFormattable]
public class PongMessage : NetworkMessage
{
    public override MessageType Type => MessageType.PONG;
}

// sync message is used for server syncing fields to clients
[ZeroFormattable]
public class SyncMessage : NetworkMessage
{
    public override MessageType Type => MessageType.SYNC;
    
    [Index(0)]
    public virtual Guid ComponentID { get; set; }
    
    [Index(1)]
    public virtual string FieldName { get; set; }
}

[ZeroFormattable]
public class NetworkStateMessage : NetworkMessage
{
    public override MessageType Type => MessageType.NETWORK_STATE;
    
    [Index(0)]
    public virtual Dictionary<Guid, NetworkIdentity> NetworkEntities { get; set; }

    [Index(1)]
    public virtual Dictionary<Guid, NetworkComponent> NetworkComponents { get; set; }
}

// uri message
[ZeroFormattable]
public class UriMessage
{
    [Index(0)]
    public virtual Uri Uri { get; set; }
}

