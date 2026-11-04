using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Reflection;

internal sealed record TypeUse(MemberInfo Member, String Verb, Type Type, String Name);
