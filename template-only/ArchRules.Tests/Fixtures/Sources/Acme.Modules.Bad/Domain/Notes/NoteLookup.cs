namespace Acme.Modules.Bad.Domain.Notes;

internal static class NoteLookup
{
    public static String? Find(String key) => key.Length == 0 ? null : key;
}
