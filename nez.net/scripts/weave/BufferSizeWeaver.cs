using System.Collections.Generic;
using Fody;

namespace nez.net.weave;

public class BufferSizeWeaver : BaseModuleWeaver
{
    public override void Execute()
    {
        foreach (var type in ModuleDefinition.GetTypes())
        {
            if (type.BaseType?.FullName == "nez.net.transport.socket.NetworkMessage")
            {
                // Handle type
            }
        }
    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        return null;
    }
}