using Mono.Cecil;
using Mono.Cecil.Cil;
using Tsikhanau.Monolith.ArchRules.Cecil;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules.Sql;

internal static class StringLiterals
{
    public static IReadOnlyList<(String TypeName, String Literal)> ReadFrom(ArchModule module)
    {
        var typeNames = module.Types
            .Select(type => type.FullName)
            .ToHashSet();

        return CecilAssemblies.Read(module.Assemblies, type => typeNames.Contains(GetOwnerName(type)), ReadLiteralsOf);
    }

    private static IEnumerable<(String TypeName, String Literal)> ReadLiteralsOf(TypeDefinition type)
    {
        var ownerName = GetOwnerName(type);
        return GetLiterals(type)
            .Select(literal => (ownerName, literal));
    }

    private static IEnumerable<String> GetLiterals(TypeDefinition type) =>
        [.. GetInstructionLiterals(type), .. GetConstantLiterals(type)];

    private static IEnumerable<String> GetInstructionLiterals(TypeDefinition type) =>
        type.BodyInstructions
            .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
            .Select(instruction => (String)instruction.Operand);

    private static IEnumerable<String> GetConstantLiterals(TypeDefinition type) =>
        type.Fields
            .Where(field => field.HasConstant)
            .Select(field => field.Constant)
            .OfType<String>();

    private static String GetOwnerName(TypeDefinition type) =>
        GetOwner(type).FullName
            .Replace('/', '+');

    private static TypeDefinition GetOwner(TypeDefinition type) =>
        type is { IsCompilerGenerated: true, DeclaringType: { } declaringType } ? GetOwner(declaringType) : type;
}
