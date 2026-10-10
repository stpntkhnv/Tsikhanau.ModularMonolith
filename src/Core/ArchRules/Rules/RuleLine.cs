namespace Tsikhanau.Monolith.ArchRules.Rules;

internal sealed class RuleLine(String text, String playbook, params String[] tests)
{
    public Boolean Covers(String test) => tests.Contains(test);

    public String Explain(String fix) =>
        playbook.Length == 0
            ? String.Join(Environment.NewLine, $"Fix: {fix}", $"Rule in AGENTS.md: {text}")
            : String.Join(
                Environment.NewLine,
                $"Fix: {fix}",
                $"Rule in AGENTS.md: {text}",
                $"Playbook: docs/playbooks/{playbook}.md");
}
