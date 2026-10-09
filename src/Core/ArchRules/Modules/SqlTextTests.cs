using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class SqlTextTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Query_handlers_only_read(String name) =>
        Check(
            SqlTextRules.QueryHandlersOnlyRead(GetModule(name)),
            $"{name} has no SQL in query handlers",
            "A query handler only reads; move the change into a command");

    [Theory]
    [EveryModule]
    public void Module_sql_names_no_other_module_schema(String name) =>
        Check(
            SqlTextRules.ModuleSqlNamesNoOtherModuleSchema(Modules, GetModule(name)),
            $"{name} has no string literals",
            "Read another module through its query facade, not its tables");

    [Theory]
    [EveryModule]
    public void Module_sql_names_its_columns(String name) =>
        Check(
            SqlTextRules.ModuleSqlNamesItsColumns(GetModule(name)),
            $"{name} has no string literals",
            "Name the columns; a column added under a running instance breaks SELECT *");
}
