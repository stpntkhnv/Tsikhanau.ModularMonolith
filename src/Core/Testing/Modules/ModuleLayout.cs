namespace Tsikhanau.Monolith.Testing.Modules;

public static class ModuleLayout
{
    private const String ContractsSuffix = ".Contracts";

    public static String GetModulesFolder(String solutionRoot) => Path.Combine(solutionRoot, "src", "Modules");

    public static String BuildRootNamespace(String appRootNamespace, String moduleName) =>
        BuildRootNamespacePrefix(appRootNamespace) + moduleName;

    public static String BuildContractsName(String moduleRootNamespace) => moduleRootNamespace + ContractsSuffix;

    internal static Boolean IsContractsName(String appRootNamespace, String projectName) =>
        projectName.StartsWith(BuildRootNamespacePrefix(appRootNamespace), StringComparison.Ordinal)
        && projectName.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    internal static String ParseModuleName(String appRootNamespace, String contractsName) =>
        contractsName[BuildRootNamespacePrefix(appRootNamespace).Length..^ContractsSuffix.Length];

    private static String BuildRootNamespacePrefix(String appRootNamespace) => $"{appRootNamespace}.Modules.";
}
