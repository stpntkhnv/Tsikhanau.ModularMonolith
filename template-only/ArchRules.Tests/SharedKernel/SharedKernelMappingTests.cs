using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Tests.SharedKernel;

public sealed class SharedKernelMappingTests
{
    private const String GoodAssemblyName = "Acme.Modules.Good";

    private const String GoodDbContextTypeName = "Acme.Modules.Good.Infrastructure.Persistence.GoodDbContext";

    private const String WidgetTypeName = "Acme.Modules.Good.Domain.Widgets.Widget";

    private const String PriceProperty = "Price";

    private const String MoneyTypeName = "Acme.SharedKernel.Money";

    private const String KernelOverride =
        "protected override IReadOnlyList<Assembly> SharedKernelAssemblies => [SharedKernelDomain.Assembly];";

    private const String UnboundPrice = "Cannot bind 'price' in 'Widget(WidgetId id, WidgetLabel label, Money price)'";

    private static Type MoneyType =>
        FixtureInputs.SharedKernelAssemblies
            .Single()
            .GetType(MoneyTypeName, throwOnError: true)!;

    private static Type MoneyConverterType =>
        typeof(ValueObjectConverter<,>).MakeGenericType(MoneyType, typeof(Decimal));

    [Fact]
    public void Db_context_maps_the_value_object_of_the_kernel_it_names_with_its_converter()
    {
        var model = BuildModel(FixtureAssemblies.Get(GoodAssemblyName));

        var priceConverter = GetPriceConverter(model);

        priceConverter.ShouldBeOfType(MoneyConverterType);
    }

    [Fact]
    public void The_same_db_context_without_the_kernel_assembly_cannot_build_its_model()
    {
        var goodAssemblyWithoutTheOverride = FixtureAssemblies.CompileWithout(GoodAssemblyName, KernelOverride);

        var exception = Should.Throw<InvalidOperationException>(() => BuildModel(goodAssemblyWithoutTheOverride));

        exception.Message.ShouldContain(UnboundPrice);
    }

    private static IModel BuildModel(Assembly goodAssembly)
    {
        var dbContextType = goodAssembly.GetType(GoodDbContextTypeName, throwOnError: true)!;
        var dbContextOptions = new DbContextOptionsBuilder(CreateEmptyOptions(dbContextType))
            .UseNpgsql()
            .Options;

        using var dbContext = (DbContext)Activator.CreateInstance(dbContextType, dbContextOptions)!;
        return dbContext.Model;
    }

    private static DbContextOptions CreateEmptyOptions(Type dbContextType) =>
        typeof(DbContextOptions<>)
            .MakeGenericType(dbContextType)
            .CreateInstance<DbContextOptions>();

    private static ValueConverter GetPriceConverter(IModel model) =>
        model
            .FindEntityType(WidgetTypeName)
            .ShouldNotBeNull()
            .FindProperty(PriceProperty)
            .ShouldNotBeNull()
            .GetValueConverter()
            .ShouldNotBeNull();
}
