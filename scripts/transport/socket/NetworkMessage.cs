[ZeroFormattable]
public class NetworkMessage
{
    [Index(0)]
    public virtual int MessageType { get; set; }
    [Index(1)]
    public virtual long Timestamp { get; set; }
    [Index(2)]
    public virtual long SequenceNumber { get; set; }
    [Index(3)]
    public virtual byte[] Payload { get; set; }
}
