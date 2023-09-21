[ZeroFormattable]
public class PlayerPositionMessage : BaseMessage
{
    [Index(1)]
    public virtual float X { get; set; }

    [Index(2)]
    public virtual float Y { get; set; }
}
