﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using nez.net.components;
using ZeroFormatter;

namespace nez.net.transport.socket;

public enum MessageType
{
    NETWORK_STATE,
    TRANSPORT,
    MIRROR,
    PING,
    PONG,
    URI
}

[Union(typeof(TransportMessage), typeof(MirrorMessage), typeof(PingMessage), typeof(PongMessage), typeof(NetworkStateMessage))]
public abstract class NetworkMessage
{
    [UnionKey]
    public abstract MessageType Type { get; }
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

