namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

internal static class SuppressionSources
{
    public const String RemovedFromCompile = $"{FeatureFolder}/Excluded.cs";

    private const String FeatureFolder = "src/Modules/Good/Acme.Modules.Good/Features/Quiet";

    private const String MigrationFolder = "src/Modules/Good/Acme.Modules.Good/Infrastructure/Migrations";

    private const String NotDotnetEfOutput = "is not a migration, its Designer part or a model snapshot";

    public static IReadOnlyList<(String Path, String Text, String Violation)> Suppressing { get; } =
    [
        CreateSource(
            $"{FeatureFolder}/NamedPragma.cs",
            5,
            "#pragma warning disable CA2016",
            """
            namespace Acme.Modules.Good.Features.Quiet;

            internal static class NamedPragma
            {
            #pragma warning disable CA2016
                public static Task RunAsync(CancellationToken cancellationToken) => Task.Delay(1);
            }
            """),
        CreateSource(
            $"{FeatureFolder}/BarePragma.cs",
            3,
            "#pragma warning disable",
            """
            namespace Acme.Modules.Good.Features.Quiet;

            #pragma warning disable

            internal static class BarePragma;
            """),
        CreateSource(
            $"{FeatureFolder}/NullableOff.cs",
            1,
            "#nullable disable",
            """
            #nullable disable

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class NullableOff
            {
                public static String Name => null;
            }
            """),
        CreateSource(
            $"{FeatureFolder}/NullableWarningsOff.cs",
            5,
            "#nullable disable warnings",
            """
            namespace Acme.Modules.Good.Features.Quiet;

            internal static class NullableWarningsOff
            {
            #nullable disable warnings
                public static String Name => null;
            #nullable restore warnings
            }
            """),
        CreateSource(
            $"{FeatureFolder}/ShortAttribute.cs",
            7,
            "[SuppressMessage]",
            """
            using System.Diagnostics.CodeAnalysis;

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class ShortAttribute
            {
                [SuppressMessage("x", "CA1031")]
                public static void Run() { }
            }
            """),
        CreateSource(
            $"{FeatureFolder}/QualifiedAttribute.cs",
            5,
            "[global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute]",
            """
            namespace Acme.Modules.Good.Features.Quiet;

            internal static class QualifiedAttribute
            {
                [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("x", "CA1031", Justification = "none")]
                public static void Run() { }
            }
            """),
        CreateSource(
            $"{FeatureFolder}/GlobalSuppressions.cs",
            3,
            "[assembly: SuppressMessage]",
            """
            using System.Diagnostics.CodeAnalysis;

            [assembly: SuppressMessage("x", "CA1031", Scope = "member", Target = "~M:Acme.Modules.Good.Quiet.Run")]
            """),
        CreateSource(
            $"{FeatureFolder}/UnconditionalAttribute.cs",
            7,
            "[UnconditionalSuppressMessage]",
            """
            using System.Diagnostics.CodeAnalysis;

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class UnconditionalAttribute
            {
                [UnconditionalSuppressMessage("x", "IL2026")]
                public static void Run() { }
            }
            """),
        CreateSource(
            $"{FeatureFolder}/AliasedAttribute.cs",
            1,
            "using Hush = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;",
            """
            using Hush = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class AliasedAttribute
            {
                [Hush("x", "CA1031")]
                public static void Run() { }
            }
            """),
        CreateSource(
            $"{FeatureFolder}/GlobalAlias.cs",
            1,
            "global using Mute = System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute;",
            """
            global using Mute = System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute;
            """),
        CreateSource(
            $"{FeatureFolder}/InactiveBranch.cs",
            6,
            "#pragma warning disable CA2016 in an inactive #if branch",
            """
            namespace Acme.Modules.Good.Features.Quiet;

            internal static class InactiveBranch
            {
            #if NEVER
            #pragma warning disable CA2016
            #endif
                public static void Run() { }
            }
            """),
        CreateSource(
            $"{FeatureFolder}/InactiveAttribute.cs",
            8,
            "[SuppressMessage(\"x\", \"CA1031\")] in an inactive #if branch",
            """
            using System.Diagnostics.CodeAnalysis;

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class InactiveAttribute
            {
            #if DEBUG
                [SuppressMessage("x", "CA1031")]
            #endif
                public static void Run() { }
            }
            """),
        CreateSource(
            RemovedFromCompile,
            3,
            "#pragma warning disable CA1031",
            """
            namespace Acme.Modules.Good.Features.Quiet;

            #pragma warning disable CA1031
            internal static class Excluded;
            """),
        CreateSource(
            $"{MigrationFolder}/MigrationHelper.cs",
            5,
            $"MigrationHelper {NotDotnetEfOutput}",
            """
            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                internal static class MigrationHelper
                {
                    public static String Quote(String name) => name;
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018070000_LateNullable.cs",
            9,
            "#nullable disable",
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                public partial class LateNullable : Migration
                {
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
            #nullable disable
                        migrationBuilder.Sql("SELECT 1");
                    }
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018080000_NullableWarnings.cs",
            3,
            "#nullable disable warnings",
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable warnings

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                public partial class NullableWarnings : Migration
                {
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                    }
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018090000_ExtraPragma.cs",
            11,
            "#pragma warning disable CS0618",
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                public partial class ExtraPragma : Migration
                {
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
            #pragma warning disable CS0618
                        migrationBuilder.Sql("SELECT 1");
            #pragma warning restore CS0618
                    }
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018100000_SecondNullable.cs",
            4,
            "#nullable disable",
            """
            #nullable disable
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                public partial class SecondNullable : Migration
                {
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.Sql("SELECT 1");
                    }
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018110000_ObsoletePragma.cs",
            11,
            "#pragma warning disable 612, 618",
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                public partial class ObsoletePragma : Migration
                {
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
            #pragma warning disable 612, 618
                        migrationBuilder.Sql("SELECT 1");
            #pragma warning restore 612, 618
                    }
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018120000_TwicePragma.Designer.cs",
            22,
            "#pragma warning disable 612, 618",
            """
            // <auto-generated />
            using Acme.Modules.Good.Infrastructure.Persistence;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                [DbContext(typeof(GoodDbContext))]
                [Migration("20261018120000_TwicePragma")]
                partial class TwicePragma
                {
                    protected override void BuildTargetModel(ModelBuilder modelBuilder)
                    {
            #pragma warning disable 612, 618
                        modelBuilder
                            .HasDefaultSchema("good")
                            .HasAnnotation("ProductVersion", "10.0.12");
            #pragma warning restore 612, 618
            #pragma warning disable 612, 618
                        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
            #pragma warning restore 612, 618
                    }
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018130000_Unpartial.Designer.cs",
            12,
            $"Unpartial {NotDotnetEfOutput}",
            """
            // <auto-generated />
            using Acme.Modules.Good.Infrastructure.Persistence;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                [DbContext(typeof(GoodDbContext))]
                [Migration("20261018130000_Unpartial")]
                class Unpartial
                {
                }
            }
            """),
        CreateSource(
            $"{MigrationFolder}/20261018140000_Uncontexted.Designer.cs",
            9,
            $"Uncontexted {NotDotnetEfOutput}",
            """
            // <auto-generated />
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                [Migration("20261018140000_Uncontexted")]
                partial class Uncontexted
                {
                }
            }
            """),
    ];

    public static IReadOnlyList<(String Path, String Text)> Clean { get; } =
    [
        (
            $"{FeatureFolder}/Spellings.cs",
            """"
            using System.Diagnostics.CodeAnalysis;

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class Spellings
            {
                public const String Regular = "#pragma warning disable CA2016 #nullable disable [SuppressMessage(\"x\", \"y\")]";

                public const String Verbatim = @"
            #pragma warning disable CA2016
            #nullable disable
            [UnconditionalSuppressMessage(""x"", ""IL2026"")]";

                public const String Raw = """
                    #pragma warning disable CA2016
                    #nullable disable warnings
                    using Hush = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
                    """;

                // #pragma warning disable CA2016
                // [SuppressMessage("x", "CA1031")]
                /*
            #pragma warning disable CA2016
            #nullable disable
            [assembly: SuppressMessage("x", "CA1031")]
                */
                [return: NotNullIfNotNull(nameof(text))]
                public static String? Echo(String? text) => text;
            }
            """"),
        (
            $"{FeatureFolder}/Enabled.cs",
            """
            #nullable enable
            using Pure = System.Diagnostics.Contracts.PureAttribute;

            namespace Acme.Modules.Good.Features.Quiet;

            internal static class Enabled
            {
            #pragma warning restore CA2016
            #nullable restore
                [Pure]
                public static String Name => "enabled";
            #if DEBUG
                public static String Build => "debug";
            #endif
            }
            """),
        (
            $"{MigrationFolder}/20261018150000_Initial.cs",
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                /// <inheritdoc />
                public partial class Initial : Migration
                {
                    /// <inheritdoc />
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.EnsureSchema(
                            name: "good");

                        migrationBuilder.CreateTable(
                            name: "widgets",
                            schema: "good",
                            columns: table => new
                            {
                                id = table.Column<Guid>(type: "uuid", nullable: false)
                            },
                            constraints: table =>
                            {
                                table.PrimaryKey("pk_widgets", x => x.id);
                            });
                    }

                    /// <inheritdoc />
                    protected override void Down(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.DropTable(
                            name: "widgets",
                            schema: "good");
                    }
                }
            }
            """),
        (
            $"{MigrationFolder}/20261018150000_Initial.Designer.cs",
            """
            // <auto-generated />
            using System;
            using Acme.Modules.Good.Infrastructure.Persistence;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;
            using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
            using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                [DbContext(typeof(GoodDbContext))]
                [Migration("20261018150000_Initial")]
                partial class Initial
                {
                    /// <inheritdoc />
                    protected override void BuildTargetModel(ModelBuilder modelBuilder)
                    {
            #pragma warning disable 612, 618
                        modelBuilder
                            .HasDefaultSchema("good")
                            .HasAnnotation("ProductVersion", "10.0.12")
                            .HasAnnotation("Relational:MaxIdentifierLength", 63);

                        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

                        modelBuilder.Entity("Acme.Modules.Good.Domain.Widgets.Widget", b =>
                            {
                                b.Property<Guid>("Id")
                                    .HasColumnType("uuid")
                                    .HasColumnName("id");

                                b.HasKey("Id")
                                    .HasName("pk_widgets");

                                b.ToTable("widgets", "good");
                            });
            #pragma warning restore 612, 618
                    }
                }
            }
            """),
        (
            $"{MigrationFolder}/GoodDbContextModelSnapshot.cs",
            """
            // <auto-generated />
            using System;
            using Acme.Modules.Good.Infrastructure.Persistence;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
            using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

            #nullable disable

            namespace Acme.Modules.Good.Infrastructure.Migrations
            {
                [DbContext(typeof(GoodDbContext))]
                partial class GoodDbContextModelSnapshot : ModelSnapshot
                {
                    protected override void BuildModel(ModelBuilder modelBuilder)
                    {
            #pragma warning disable 612, 618
                        modelBuilder
                            .HasDefaultSchema("good")
                            .HasAnnotation("ProductVersion", "10.0.12")
                            .HasAnnotation("Relational:MaxIdentifierLength", 63);

                        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

                        modelBuilder.Entity("Acme.Modules.Good.Domain.Widgets.Widget", b =>
                            {
                                b.Property<Guid>("Id")
                                    .HasColumnType("uuid")
                                    .HasColumnName("id");

                                b.HasKey("Id")
                                    .HasName("pk_widgets");

                                b.ToTable("widgets", "good");
                            });
            #pragma warning restore 612, 618
                    }
                }
            }
            """),
    ];

    private static (String Path, String Text, String Violation) CreateSource(
        String path,
        Int32 line,
        String finding,
        String text) =>
        (path, text, $"{path}:{line} {finding}");
}
