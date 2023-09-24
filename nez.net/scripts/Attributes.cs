using System;

namespace nez.net;

[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute
{
    
}

[AttributeUsage(AttributeTargets.Field)]
public class SyncAttribute : Attribute
{
    internal object PreviousValue;
    
    public void Sync(object previousValue, object currentValue)
    {
        if(previousValue == currentValue)
            return;
        
        PreviousValue = previousValue;
        
        // TODO: send sync message
    }
}