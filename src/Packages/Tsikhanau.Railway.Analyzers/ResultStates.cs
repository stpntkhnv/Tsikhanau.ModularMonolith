namespace Tsikhanau.Railway.Analyzers;

internal sealed class ResultStates
{
    private readonly Dictionary<AccessPath, Boolean> _succeeded;

    public ResultStates()
        : this(new Dictionary<AccessPath, Boolean>(), isUnreachable: false)
    {
    }

    private ResultStates(Dictionary<AccessPath, Boolean> succeeded, Boolean isUnreachable)
    {
        _succeeded = succeeded;
        IsUnreachable = isUnreachable;
    }

    public Boolean IsUnreachable { get; private set; }

    public ResultStates Clone() => new(new Dictionary<AccessPath, Boolean>(_succeeded), IsUnreachable);

    public Boolean? Get(AccessPath path) => _succeeded.TryGetValue(path, out var succeeded) ? succeeded : null;

    public void Set(AccessPath path, Boolean succeeded) => _succeeded[path] = succeeded;

    public void Forget(AccessPath? path)
    {
        if (path is null)
        {
            return;
        }

        foreach (var key in _succeeded.Keys.Where(key => key.StartsWith(path)).ToList())
        {
            _succeeded.Remove(key);
        }
    }

    public void Clear() => _succeeded.Clear();

    public void MarkUnreachable() => IsUnreachable = true;

    public Boolean IntersectWith(ResultStates other)
    {
        var conflicting = _succeeded
            .Where(entry => other.Get(entry.Key) != entry.Value)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var key in conflicting)
        {
            _succeeded.Remove(key);
        }

        return conflicting.Count > 0;
    }
}
