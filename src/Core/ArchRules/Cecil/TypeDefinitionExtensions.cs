using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Tsikhanau.Monolith.ArchRules.Cecil;

internal static class TypeDefinitionExtensions
{
    extension(TypeDefinition type)
    {
        public IEnumerable<MethodDefinition> MethodsWithBodies => type.Methods.Where(method => method.HasBody);

        public IEnumerable<Instruction> BodyInstructions =>
            type.MethodsWithBodies.SelectMany(method => method.Body.Instructions);

        public TypeDefinition TopLevelType =>
            type.DeclaringType is { } declaringType ? declaringType.TopLevelType : type;
    }
}
