namespace Tsikhanau.Monolith.Testing.Modules;

public static class ModuleLayout
{
    public static String GetModulesFolder(String solutionRoot) => Path.Combine(solutionRoot, "src", "Modules");

    public static String BuildRootNamespace(String appRootNamespace, String moduleName) =>
        BuildRootNamespacePrefix(appRootNamespace) + moduleName;

    public static String BuildContractsName(String moduleRootNamespace) => $"{moduleRootNamespace}.Contracts";

    internal static String BuildRootNamespacePrefix(String appRootNamespace) => $"{appRootNamespace}.Modules.";
}
