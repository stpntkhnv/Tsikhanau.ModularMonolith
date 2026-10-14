using System.Text.Json;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal static class NameCasingExtensions
{
    extension(String name)
    {
        public String ToSnakeCase() =>
            String.Concat(name.Select((letter, index) => ToSnakeCasePart(name, letter, index)));

        public String ToKebabCase() => JsonNamingPolicy.KebabCaseLower.ConvertName(name);

        public String ToCamelCase() => JsonNamingPolicy.CamelCase.ConvertName(name);

        public Boolean StartsWithWord(String word) =>
            name.Length > word.Length
            && name.StartsWith(word, StringComparison.Ordinal)
            && Char.IsUpper(name[word.Length]);
    }

    private static String ToSnakeCasePart(String name, Char letter, Int32 index) =>
        StartsWord(name, index) ? $"_{Char.ToLowerInvariant(letter)}" : $"{Char.ToLowerInvariant(letter)}";

    private static Boolean StartsWord(String name, Int32 index) =>
        index > 0 && Char.IsUpper(name[index]) && (Char.IsLower(name[index - 1]) || EndsAcronym(name, index));

    private static Boolean EndsAcronym(String name, Int32 index) =>
        Char.IsUpper(name[index - 1]) && index + 1 < name.Length && Char.IsLower(name[index + 1]);
}
