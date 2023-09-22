using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;

namespace nez.net.weave;

public class NezNetWeaver : BaseModuleWeaver
{
    public override void Execute()
    {
        foreach (var type in ModuleDefinition.Types)
        {
            foreach (var method in type.Methods)
            {
                if (MethodHasCommandAttribute(method))
                {
                    CacheMethodById(method);
                }
            }
        }
    }

    private bool MethodHasCommandAttribute(MethodDefinition method)
    {
        // Check if the method has [Command] attribute
        return method.CustomAttributes
            .Any(attr => attr.AttributeType.FullName == "nez.net.CommandAttribute");
    }

    private void CacheMethodById(MethodDefinition method)
    {
        // Generate code to cache the method by some ID
        // For example, you could generate a static constructor that populates a Dictionary
    }
    
    public override IEnumerable<string> GetAssembliesForScanning()
    {
        return Enumerable.Empty<string>();
    }
}