using System;
using System.Collections.Generic;
using System.Linq;

namespace Vorax;

public static class Registry
{
    static readonly Lazy<IReadOnlyList<Strategy>> Found = new(Discover);

    public static IReadOnlyList<Strategy> All() => Found.Value;

    public static Strategy? ByName(string? name) => string.IsNullOrEmpty(name) ? null : Found.Value.FirstOrDefault(s => s.Name == name);

    static IReadOnlyList<Strategy> Discover() =>
    [
        .. typeof(Strategy).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Strategy)) && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (Strategy)Activator.CreateInstance(t)!)
            .OrderBy(s => s.Name, StringComparer.Ordinal),
    ];
}
