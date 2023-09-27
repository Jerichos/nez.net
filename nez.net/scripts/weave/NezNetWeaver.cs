using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace nez.net.weave;

public class NezNetWeaver : BaseModuleWeaver
{
    public override void Execute()
    {
        System.Diagnostics.Debugger.Launch();
        WriteDebug("Entering Execute");

        foreach (var type in ModuleDefinition.GetTypes())
        {
            WriteDebug($"Processing type {type.Name}");
            foreach (var method in type.Methods)
            {
                WriteDebug($"Checking method {method.Name}");
                if (method.HasCustomAttributes && method.CustomAttributes.Any(a => a.AttributeType.Name == "CommandAttribute"))
                {
                    WriteDebug("Found method with CommandAttribute");
                
                    // Remove the original method body
                    method.Body.Instructions.Clear();

                    var sendCommandMethod = FindMethodInTypeAndBaseTypes(type, "SendCommandMessageInternal");
                    if (sendCommandMethod != null)
                    {
                        var importedMethod = ModuleDefinition.ImportReference(sendCommandMethod);
                        var processor = method.Body.GetILProcessor();
                        processor.Append(processor.Create(OpCodes.Call, importedMethod));
                        processor.Append(processor.Create(OpCodes.Ret));
                    }
                    else
                    {
                        WriteDebug($"Could not find SendCommandMessageInternal in type {type.Name} or its base types");
                    }

                }
            }
        }
    }
    
    public MethodDefinition FindMethodInTypeAndBaseTypes(TypeDefinition type, string methodName)
    {
        while (type != null)
        {
            var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType?.Resolve();
        }
        return null;
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
    
    public override bool ShouldCleanReference => true;
}