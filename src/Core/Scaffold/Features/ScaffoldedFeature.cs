using Tsikhanau.Monolith.Scaffold.Modules;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record ScaffoldedFeature(ScaffoldedModule Module, FeatureRequest Request)
{
    public const String TestProjectFolder = "src/Tests/IntegrationTests";

    public String Name => Request.Name;

    public EndpointMethod Method => Request.Method;

    public Boolean HasRequestBody => Method is EndpointMethod.Post or EndpointMethod.Put;

    public String Namespace => $"{Module.Namespace}.Features.{Name}";

    public String FeaturesFolder => Module.InProject("Features");

    public String Folder => $"{FeaturesFolder}/{Name}";

    public String Command => $"{Name}Command";

    public String CommandHandler => $"{Name}CommandHandler";

    public String RequestRecord => $"{Name}Request";

    public String Endpoint => $"{Name}Endpoint";

    public String Route => $"/{Name.ToKebabCase()}";

    public String RouteName => $"{Module.Name}.{Name}";

    public String HttpPath => $"/{Module.Route}{Route}";

    public String FacadeParameter => Module.ModuleClass.ToCamelCase();

    public String NotImplementedCode => $"{Name}.NotImplemented";

    public String NotImplementedMessage => $"{Name} in {Module.Name} is not implemented";

    public String TestClass => $"{Name}Tests";

    public String TestNamespace => $"{Module.App.RootNamespace}.IntegrationTests.Modules.{Module.Name}";

    public String TestPath => $"{TestProjectFolder}/Modules/{Module.Name}/{TestClass}.cs";

    public String InFeature(String fileName) => $"{Folder}/{fileName}";
}
