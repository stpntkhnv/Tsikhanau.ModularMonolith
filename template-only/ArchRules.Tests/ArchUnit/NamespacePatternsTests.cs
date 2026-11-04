using System.Text.RegularExpressions;
using Tsikhanau.Monolith.ArchRules.ArchUnit;
using Tsikhanau.Monolith.ArchRules.Inputs;

namespace Tsikhanau.Monolith.ArchRules.Tests.ArchUnit;

public sealed class NamespacePatternsTests
{
    private static readonly ArchModule NotesModule = new("Notes", "Acme.Modules.Notes", () => []);

    [Theory]
    [InlineData("Acme.Modules.Notes", true)]
    [InlineData("Acme.Modules.Notes.Domain", true)]
    [InlineData("Acme.Modules.Notes.Domain.Notes", true)]
    [InlineData("Acme.Modules.Notes.Contracts", true)]
    [InlineData("Acme.Modules.Notes.Contracts.Events", true)]
    [InlineData("Acme.Modules.NotesArchive", false)]
    [InlineData("Acme.ModulesNotes", false)]
    [InlineData("Acme.Modules", false)]
    [InlineData("Acme", false)]
    public void Root_pattern_matches_the_module_root_and_its_children(String namespaceName, Boolean isMatch) =>
        Regex
            .IsMatch(namespaceName, NotesModule.RootPattern)
            .ShouldBe(isMatch);

    [Theory]
    [InlineData("Acme.Modules.Notes", true)]
    [InlineData("Acme.Modules.Notes.Domain", true)]
    [InlineData("Acme.Modules.Notes.Domain.Contracts", true)]
    [InlineData("Acme.Modules.Notes.ContractsArchive", true)]
    [InlineData("Acme.Modules.Notes.Contracts", false)]
    [InlineData("Acme.Modules.Notes.Contracts.Events", false)]
    [InlineData("Acme.Modules.NotesArchive", false)]
    [InlineData("Acme.Modules.NotesArchive.Contracts", false)]
    [InlineData("Acme.Modules", false)]
    public void Outside_contracts_pattern_matches_the_module_except_its_contracts(
        String namespaceName,
        Boolean isMatch) =>
        Regex
            .IsMatch(namespaceName, NotesModule.OutsideContractsPattern)
            .ShouldBe(isMatch);

    [Theory]
    [InlineData("Acme.Modules.Notes.Domain", true)]
    [InlineData("Acme.Modules.Notes.Domain.Notes", true)]
    [InlineData("Acme.Modules.Notes.Infrastructure", true)]
    [InlineData("Acme.Modules.Notes.Infrastructure.Persistence", true)]
    [InlineData("Acme.Modules.Notes", false)]
    [InlineData("Acme.Modules.Notes.DomainEvents", false)]
    [InlineData("Acme.Modules.Notes.Features", false)]
    [InlineData("Acme.Modules.Notes.Features.Domain", false)]
    [InlineData("Acme.Modules.NotesArchive.Domain", false)]
    public void Layer_pattern_matches_the_named_layers_and_their_children(String namespaceName, Boolean isMatch) =>
        Regex
            .IsMatch(namespaceName, NotesModule.BuildLayerPattern("Domain", "Infrastructure"))
            .ShouldBe(isMatch);

    [Theory]
    [InlineData("Microsoft.Extensions.Configuration", true)]
    [InlineData("Microsoft.Extensions.Configuration.Binder", true)]
    [InlineData("Microsoft.Extensions.ConfigurationBinder", false)]
    [InlineData("Microsoft.Extensions", false)]
    [InlineData("Microsoft.Extensions.Options", false)]
    public void Pattern_of_one_namespace_matches_it_and_its_children(String namespaceName, Boolean isMatch) =>
        Regex
            .IsMatch(namespaceName, NamespacePatterns.Build("Microsoft.Extensions.Configuration"))
            .ShouldBe(isMatch);

    [Theory]
    [InlineData("Microsoft", true)]
    [InlineData("Microsoft.Extensions.Configuration", true)]
    [InlineData("Npgsql", true)]
    [InlineData("NpgsqlTypes", true)]
    [InlineData("Npgsql.Internal", true)]
    [InlineData("Dapper", true)]
    [InlineData("NpgsqlTools", false)]
    [InlineData("MicrosoftTools", false)]
    [InlineData("DapperTools", false)]
    [InlineData("System", false)]
    public void Pattern_of_several_namespaces_matches_each_and_their_children(String namespaceName, Boolean isMatch) =>
        Regex
            .IsMatch(namespaceName, NamespacePatterns.Build("Microsoft", "Npgsql", "NpgsqlTypes", "Dapper"))
            .ShouldBe(isMatch);

    [Theory]
    [InlineData("Tsikhanau.Monolith", true)]
    [InlineData("Tsikhanau.Monolith.Domain", true)]
    [InlineData("Tsikhanau.Monolith.Domain.Shared", true)]
    [InlineData("Tsikhanau.Monolith.SharedKernel", true)]
    [InlineData("Tsikhanau.Monolith.Shared", false)]
    [InlineData("Tsikhanau.Monolith.Shared.Reflection", false)]
    [InlineData("Tsikhanau.MonolithTools", false)]
    [InlineData("Tsikhanau.Railway", false)]
    public void Except_pattern_matches_the_parent_and_its_children_except_one_child(
        String namespaceName,
        Boolean isMatch) =>
        Regex
            .IsMatch(namespaceName, NamespacePatterns.BuildExcept("Tsikhanau.Monolith", "Shared"))
            .ShouldBe(isMatch);
}
