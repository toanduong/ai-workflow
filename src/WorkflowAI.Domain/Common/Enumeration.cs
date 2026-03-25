using System.Reflection;

namespace WorkflowAI.Domain.Common;

public abstract class Enumeration<TEnum> : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> _enumerations = new(CreateEnumerations);

    public int Id { get; }
    public string Name { get; }

    protected Enumeration(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public static TEnum? FromId(int id) =>
        _enumerations.Value.GetValueOrDefault(id);

    public static TEnum? FromName(string name) =>
        _enumerations.Value.Values.SingleOrDefault(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyCollection<TEnum> GetAll() =>
        _enumerations.Value.Values.ToList().AsReadOnly();

    public bool Equals(Enumeration<TEnum>? other)
    {
        if (other is null) return false;
        return GetType() == other.GetType() && Id == other.Id;
    }

    public override bool Equals(object? obj) => Equals(obj as Enumeration<TEnum>);
    public override int GetHashCode() => Id.GetHashCode();
    public override string ToString() => Name;

    private static Dictionary<int, TEnum> CreateEnumerations()
    {
        return typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => (TEnum)f.GetValue(null)!)
            .ToDictionary(e => e.Id);
    }
}
