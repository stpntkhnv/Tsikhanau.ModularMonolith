using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class FacadeTemplate(ScaffoldedModule module)
{
    public NewFile FacadeContractFile =>
        new(
            module.InProject($"{module.FacadeContract}.cs"),
            $$"""
            {{Usings("Tsikhanau.Monolith.Application.Modules")}}

            namespace {{module.Namespace}};

            {{FacadeContractDeclaration}}

            """);

    public NewFile FacadeClassFile =>
        new(
            module.InProject($"Infrastructure/{module.FacadeClass}.cs"),
            $$"""
            {{Usings("Microsoft.Extensions.DependencyInjection", "Tsikhanau.Monolith.Infrastructure.Dispatching")}}

            namespace {{module.Namespace}}.Infrastructure;

            {{FacadeClassDeclaration}}

            """);

    private String FacadeContractDeclaration =>
        FirstThatFits(
            $"internal interface {module.FacadeContract} : IModuleFacade;",
            $"""
             internal interface {module.FacadeContract}
                 : IModuleFacade;
             """);

    private String FacadeClassDeclaration =>
        FirstThatFits(
            $"""
             internal sealed class {module.FacadeClass}(IServiceScopeFactory serviceScopeFactory)
                 : ModuleFacade<{module.FacadeContract}>(serviceScopeFactory), {module.FacadeContract};
             """,
            $"""
             internal sealed class {module.FacadeClass}(IServiceScopeFactory serviceScopeFactory)
                 : ModuleFacade<{module.FacadeContract}>(serviceScopeFactory),
                     {module.FacadeContract};
             """,
            $"""
             internal sealed class {module.FacadeClass}(
                 IServiceScopeFactory serviceScopeFactory)
                 : ModuleFacade<{module.FacadeContract}>(serviceScopeFactory),
                     {module.FacadeContract};
             """);
}
