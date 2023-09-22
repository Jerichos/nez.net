using ZeroFormatter;

namespace nez.net.transport.socket;

[ZeroFormattable]
public class TransportMessage
{
    [Index(0)]
    public virtual TransportCode Code { get; set; }
}