using nez.net.transport.socket;
using ZeroFormatter;

[ZeroFormattable]
public class PlayerPositionMessage : NetworkMessage
{
    [Index(1)]
    public virtual float X { get; set; }

    [Index(2)]
    public virtual float Y { get; set; }
}
