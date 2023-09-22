using ZeroFormatter;

namespace nez.net.transport.socket;

[ZeroFormattable]
public class NetworkMessage
{
    [Index(0)]
    public virtual uint NetworkID { get; set; }
    [Index(1)]
    public virtual long Timestamp { get; set; }
    [Index(2)]
    public virtual long SequenceNumber { get; set; }
    [Index(3)]
    public virtual byte[] Payload { get; set; }
}