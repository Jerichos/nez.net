using System.Collections.Generic;
using Nez;
using Nez.AI.GOAP;

namespace nez.net.components;

public abstract class NetworkComponent : Component
{
    public uint ComponentID { get; private set; }
    
    public bool IsServet { get; private set; }
    public bool IsClient { get; private set; }
    public bool IsLocalPlayer { get; private set; }

    private static Dictionary<string, Action> _commandHandlers;
    
    // called by weaver
    protected void SendCommandInternal(string functionFullName, int functionHashCode)
    {
        CommandMessage commandMessage = new CommandMessage
        {
            
        };
    }
}