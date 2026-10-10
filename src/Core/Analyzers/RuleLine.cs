namespace Tsikhanau.Monolith.Analyzers;

internal sealed class RuleLine(String text, String playbook, params String[] diagnostics)
{
    public Boolean Covers(String diagnostic) => diagnostics.Contains(diagnostic);

    public String Explain(String fix) =>
        playbook.Length == 0
            ? $"{fix}. Rule in AGENTS.md: {Escape(text)}"
            : $"{fix}. Rule in AGENTS.md: {Escape(text)} Playbook: docs/playbooks/{playbook}.md";

    private static String Escape(String messagePart) =>
        messagePart
            .Replace("{", "{{")
            .Replace("}", "}}");
}
