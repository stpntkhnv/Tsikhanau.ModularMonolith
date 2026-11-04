using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Tsikhanau.Monolith.ArchRules.Cecil;

internal static class TypeDefinitionExtensions
{
    extension(TypeDefinition type)
    {
        public IEnumerable<Instruction> BodyInstructions =>
            type.Methods
                .Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions);

        public TypeDefinition TopLevelType =>
            type.DeclaringType is { } declaringType ? declaringType.TopLevelType : type;
    }
}
