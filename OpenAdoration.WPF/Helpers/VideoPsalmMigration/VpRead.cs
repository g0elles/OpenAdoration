namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>
/// Accessors over the plain object graph <c>VpJsonReader</c> produces (Dictionary / List /
/// string / double / bool / null). Numbers come back as <see cref="double"/>.
/// </summary>
internal static class VpRead
{
    public static IReadOnlyDictionary<string, object?>? AsDict(object? value) =>
        value as Dictionary<string, object?>;

    public static string? GetString(IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) ? value as string : null;

    public static IEnumerable<object?> GetArray(IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) && value is List<object?> list ? list : [];

    public static int? GetInt(IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) && value is double d ? (int)d : null;

    public static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
