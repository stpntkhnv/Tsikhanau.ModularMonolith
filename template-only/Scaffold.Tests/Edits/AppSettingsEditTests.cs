using System.Text;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class AppSettingsEditTests
{
    private const String QueuesEnd = "      \"ClaimBatchSize\": 16\n    }\n";

    private const String LayoutRule = "with one entry per line and its closing brace on a line of its own";

    [Fact]
    public void First_module_opens_the_modules_section_after_the_last_app_section()
    {
        var notes = SampleModules.Create("Notes");

        var edited = ReadText(AppSettingsEdit.AddModule(notes.App.AppSettings, notes));

        edited.ShouldBe(
            WithModules(
                """
                    "Modules": {
                      "Notes": {}
                    }
                """));
    }

    [Fact]
    public void Second_module_that_sorts_before_the_first_goes_before_it()
    {
        var billing = SampleModules.Create("Billing");
        var appSettings = CreateAppSettings(
            WithModules(
                """
                    "Modules": {
                      "Notes": {}
                    }
                """));

        var edited = ReadText(AppSettingsEdit.AddModule(appSettings, billing));

        edited.ShouldBe(
            WithModules(
                """
                    "Modules": {
                      "Billing": {},
                      "Notes": {}
                    }
                """));
    }

    [Fact]
    public void Second_module_that_sorts_after_the_first_goes_after_it()
    {
        var tags = SampleModules.Create("Tags");
        var appSettings = CreateAppSettings(
            WithModules(
                """
                    "Modules": {
                      "Notes": {
                        "Limit": 10
                      }
                    }
                """));

        var edited = ReadText(AppSettingsEdit.AddModule(appSettings, tags));

        edited.ShouldBe(
            WithModules(
                """
                    "Modules": {
                      "Notes": {
                        "Limit": 10
                      },
                      "Tags": {}
                    }
                """));
    }

    [Theory]
    [InlineData("    \"Modules\": {}")]
    [InlineData("    \"Modules\": { }")]
    [InlineData("    \"Modules\": {\n    }")]
    public void Empty_modules_section_gets_the_module_on_a_line_of_its_own(String emptyModules)
    {
        var notes = SampleModules.Create("Notes");
        var appSettings = CreateAppSettings(WithModules(emptyModules));

        var edited = ReadText(AppSettingsEdit.AddModule(appSettings, notes));

        edited.ShouldBe(
            WithModules(
                """
                    "Modules": {
                      "Notes": {}
                    }
                """));
    }

    [Theory]
    [InlineData("\"Tags\": {}", "\"Tags\": {},")]
    [InlineData("\"Tags\": {},", "\"Tags\": {},")]
    [InlineData("\"Tags\": {} // tags, the last one", "\"Tags\": {}, // tags, the last one")]
    [InlineData("\"Tags\": {} /* tags, the last one */", "\"Tags\": {}, /* tags, the last one */")]
    [InlineData("\"Tags\": {}, // tags, the last one", "\"Tags\": {}, // tags, the last one")]
    [InlineData("\"Tags\": {} /* tags, the last one */,", "\"Tags\": {} /* tags, the last one */,")]
    [InlineData("\"Tags\": {}\n      // more modules, later", "\"Tags\": {},\n      // more modules, later")]
    public void Module_after_the_last_one_adds_a_comma_only_where_there_is_none(
        String lastModule,
        String expectedLastModule)
    {
        var zones = SampleModules.Create("Zones");
        var appSettings = CreateAppSettings(WithModules($"    \"Modules\": {{\n      {lastModule}\n    }}"));

        var edited = ReadText(AppSettingsEdit.AddModule(appSettings, zones));

        edited.ShouldBe(WithModules($"    \"Modules\": {{\n      {expectedLastModule}\n      \"Zones\": {{}}\n    }}"));
    }

    [Fact]
    public void Modules_section_after_a_trailing_comma_keeps_the_comments_and_the_commas()
    {
        var notes = SampleModules.Create("Notes");
        var appSettings = CreateAppSettings(
            """
            {
              // Acme settings, edited by hand
              "Logging": {
                "LogLevel": {
                  "Default": "Information", /* the rest, as the template has it */
                },
              },
              "App": {
                "Database": {
                  "MaxConnections": 20, // per instance
                },
              },
            }

            """);

        var edited = ReadText(AppSettingsEdit.AddModule(appSettings, notes));

        edited.ShouldBe(
            """
            {
              // Acme settings, edited by hand
              "Logging": {
                "LogLevel": {
                  "Default": "Information", /* the rest, as the template has it */
                },
              },
              "App": {
                "Database": {
                  "MaxConnections": 20, // per instance
                },
                "Modules": {
                  "Notes": {}
                }
              },
            }

            """);
    }

    [Fact]
    public void Module_among_commented_modules_keeps_the_comments_and_the_commas()
    {
        var notes = SampleModules.Create("Notes");
        var appSettings = CreateAppSettings(
            WithModules(
                """
                    "Modules": {
                      "Billing": {}, // billing, first
                      "Tags": { /* none yet, see the wiki */ },
                    }
                """));

        var edited = ReadText(AppSettingsEdit.AddModule(appSettings, notes));

        edited.ShouldBe(
            WithModules(
                """
                    "Modules": {
                      "Billing": {}, // billing, first
                      "Notes": {},
                      "Tags": { /* none yet, see the wiki */ },
                    }
                """));
    }

    [Fact]
    public void File_with_crlf_line_endings_keeps_them()
    {
        var notes = SampleModules.Create("Notes");
        var lfAppSettings = ReadText(AppSettingsEdit.AddModule(notes.App.AppSettings, notes));
        var crlfAppSettings = CreateAppSettings(SampleApp.AppSettings.ReplaceLineEndings("\r\n"));

        var edited = ReadText(AppSettingsEdit.AddModule(crlfAppSettings, notes));

        edited.ShouldBe(lfAppSettings.ReplaceLineEndings("\r\n"));
    }

    [Theory]
    [InlineData("    \"Modules\": {\n      \"Notes\": {}\n    }", "no App:Modules:Notes yet")]
    [InlineData("    \"Modules\": {\n      \"notes\": { \"Limit\": 10 }\n    }", "no App:Modules:Notes yet")]
    [InlineData("    \"Modules\": []", "App:Modules as an object")]
    [InlineData("    \"Modules\": { \"Tags\": {} }", $"App:Modules {LayoutRule}")]
    [InlineData("    \"Modules\": { \"Billing\": {} }", $"App:Modules {LayoutRule}")]
    [InlineData("    \"Modules\": { /* none yet */ }", $"App:Modules {LayoutRule}")]
    public void Modules_section_the_edit_cannot_take_the_module_into_is_refused(String modules, String expected)
    {
        var notes = SampleModules.Create("Notes");
        var appSettings = CreateAppSettings(WithModules(modules));

        var message = ReadMessage(AppSettingsEdit.AddModule(appSettings, notes));

        message.ShouldBe($"{SampleApp.AppSettingsPath} must hold {expected}");
    }

    [Theory]
    [InlineData("{\n  \"Logging\": {}\n}\n", "an object App")]
    [InlineData("{\n  \"App\": []\n}\n", "an object App")]
    [InlineData("{\n  \"App\": { \"Database\": { \"MaxConnections\": 20 } }\n}\n", $"App {LayoutRule}")]
    public void App_section_the_edit_cannot_take_the_modules_into_is_refused(String text, String expected)
    {
        var notes = SampleModules.Create("Notes");

        var message = ReadMessage(AppSettingsEdit.AddModule(CreateAppSettings(text), notes));

        message.ShouldBe($"{SampleApp.AppSettingsPath} must hold {expected}");
    }

    private static String WithModules(String modules) =>
        SampleApp.AppSettings.Replace(
            QueuesEnd,
            $"      \"ClaimBatchSize\": 16\n    }},\n{modules}\n",
            StringComparison.Ordinal);

    private static TextFile CreateAppSettings(String text) =>
        SampleModules.CreateFile(SampleApp.AppSettingsPath, text);

    private static String ReadText(Result<FileEdit> edit)
    {
        edit.IsSuccess.ShouldBeTrue(ReadMessage(edit));
        return Encoding.UTF8.GetString(edit.Value.Updated);
    }

    private static String ReadMessage(Result<FileEdit> edit) => edit.Match(_ => "success", error => error.Message);
}
