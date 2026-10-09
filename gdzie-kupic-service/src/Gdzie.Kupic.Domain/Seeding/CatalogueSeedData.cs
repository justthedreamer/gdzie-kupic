namespace Gdzie.Kupic.Domain.Seeding;

/// <summary>
/// The agreed catalogue seed list (see docs/catalogue-seed-list.md). Identifiers are derived
/// deterministically from the names below, so they are identical on every fresh database. Do not
/// change a name here to rename a seeded row - that would produce a new ID; use the admin API.
/// </summary>
public static class CatalogueSeedData
{
    public sealed record SeedTag(Guid Id, string Name);

    public sealed record SeedCategory(Guid Id, string Name, IReadOnlyList<SeedTag> Tags);

    public static readonly IReadOnlyList<SeedCategory> Categories = Build(new Dictionary<string, string[]>
    {
        ["Elektronika"] = ["Telefon", "Laptop", "Tablet", "Telewizor", "Słuchawki", "Aparat fotograficzny", "Konsola do gier"],
        ["AGD"] = ["Lodówka", "Pralka", "Zmywarka", "Odkurzacz", "Ekspres do kawy", "Mikrofalówka"],
        ["Dom i ogród"] = ["Narzędzia ręczne", "Elektronarzędzia", "Kosiarka", "Meble ogrodowe", "Oświetlenie", "Farby i lakiery"],
        ["Motoryzacja"] = ["Opony", "Akumulator", "Olej silnikowy", "Części zamienne", "Akcesoria samochodowe"],
        ["Sport i turystyka"] = ["Rower", "Namiot", "Plecak turystyczny", "Sprzęt fitness", "Buty sportowe"],
        ["Dziecko"] = ["Wózek", "Fotelik samochodowy", "Zabawki", "Ubranka"],
        ["Zdrowie i uroda"] = ["Kosmetyki", "Perfumy", "Suplementy", "Sprzęt medyczny"],
        ["Muzyka i hobby"] = ["Mikrofon", "Gitara", "Keyboard", "Książki", "Gry planszowe"],
        ["Biuro i szkoła"] = ["Papier", "Drukarka", "Artykuły piśmiennicze", "Plecak szkolny"],
    });

    private static IReadOnlyList<SeedCategory> Build(Dictionary<string, string[]> source) =>
        source.Select(c => new SeedCategory(
                StableId($"category:{c.Key}"),
                c.Key,
                c.Value.Select(t => new SeedTag(StableId($"tag:{c.Key}:{t}"), t)).ToList()))
            .ToList();

    private static Guid StableId(string key) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
}
