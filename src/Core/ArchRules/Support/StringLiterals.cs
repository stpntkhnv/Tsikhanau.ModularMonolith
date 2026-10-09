using Mono.Cecil;
using Mono.Cecil.Cil;
using Tsikhanau.Monolith.ArchRules.Inputs;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class StringLiterals
{
    public static IReadOnlyList<(String TypeName, String Literal)> ReadFrom(ArchModule module)
    {
        var typeNames = module.Types
            .Select(type => type.FullName)
            .ToHashSet();

        return module.Assemblies
            .Select(assembly => assembly.Location)
            .Distinct()
            .SelectMany(location => ReadFrom(location, typeNames))
            .ToList();
    }

    private static List<(String TypeName, String Literal)> ReadFrom(String location, HashSet<String?> typeNames)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(location);
        return assembly.MainModule
            .GetTypes()
            .Where(type => typeNames.Contains(GetOwnerName(type)))
            .SelectMany(ReadLiteralsOf)
            .ToList();
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
        type.Methods
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
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
