using Microsoft.CodeAnalysis;

namespace Tsikhanau.Monolith.Analyzers;

internal static class SyntaxTokenExtensions
{
    extension(SyntaxToken token)
    {
        public Int32 StartLine => token.LineSpan.StartLinePosition.Line;

        public Int32 EndLine => token.LineSpan.EndLinePosition.Line;

        private FileLinePositionSpan LineSpan =>
            token
                .GetLocation()
                .GetLineSpan();
    }
}
