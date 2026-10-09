namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class ErrorsAsValuesAnalyzerTests
{
    [Fact]
    public async Task Throw_statement_in_domain_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            using System;
            using System.Threading.Tasks;

            namespace App.Modules.Notes.Domain.Notes;

            class Note
            {
                void Rename(String title)
                {
                    throw new ArgumentException(title);
                }
            }
            """);

        lines.ShouldBe([10]);
    }

    [Fact]
    public async Task Throw_in_an_async_handler_lambda_and_expression_in_features_is_reported()
    {
        var lines = await GetReportedLinesAsync(
            """
            using System;
            using System.Threading.Tasks;

            namespace App.Modules.Notes.Features.CreateNote;

            class CreateNoteCommandHandler
            {
                async Task Handle(String title)
                {
                    await Task.Yield();
                    throw new InvalidOperationException();
                }

                Func<String> Read(String? title) => () => title ?? throw new ArgumentException();

                void Run(Action action) => Run(() => throw new InvalidOperationException());
            }
            """);

        lines.ShouldBe([11, 14, 16]);
    }

    [Fact]
    public async Task Await_using_in_features_passes()
    {
        var lines = await GetReportedLinesAsync(
            """
            using System;
            using System.Threading.Tasks;

            namespace App.Modules.Notes.Features.GetNote;

            class GetNoteQueryHandler
            {
                async Task<Int64> Handle()
                {
                    await using var stream = new System.IO.MemoryStream();
                    return stream.Length;
                }
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Throw_outside_domain_and_features_passes()
    {
        var lines = await GetReportedLinesAsync(
            """
            using System;
            using System.Threading.Tasks;

            namespace App.Modules.Notes.Infrastructure;

            class NotesInfrastructureRegistration
            {
                void Register(String name) => throw new InvalidOperationException(name);
            }
            """);

        lines.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String source) =>
        AnalyzerRunner.GetReportedLinesAsync(new ErrorsAsValuesAnalyzer(), source);
}
