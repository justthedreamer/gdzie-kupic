# Catalogue Seed List

Categories and tags created automatically at service startup (see [design-decisions.md §8](design-decisions.md#8-category-system)). Provisioning is idempotent and keyed by stable IDs, so admin renames and disabled flags on seeded rows are never overwritten. The source of truth in code is `CatalogueSeedData` (`Gdzie.Kupic.Domain/Seeding`); IDs are derived deterministically from the names and are identical in every environment.

## Elektronika
- Telefon
- Laptop
- Tablet
- Telewizor
- Słuchawki
- Aparat fotograficzny
- Konsola do gier

## AGD
- Lodówka
- Pralka
- Zmywarka
- Odkurzacz
- Ekspres do kawy
- Mikrofalówka

## Dom i ogród
- Narzędzia ręczne
- Elektronarzędzia
- Kosiarka
- Meble ogrodowe
- Oświetlenie
- Farby i lakiery

## Motoryzacja
- Opony
- Akumulator
- Olej silnikowy
- Części zamienne
- Akcesoria samochodowe

## Sport i turystyka
- Rower
- Namiot
- Plecak turystyczny
- Sprzęt fitness
- Buty sportowe

## Dziecko
- Wózek
- Fotelik samochodowy
- Zabawki
- Ubranka

## Zdrowie i uroda
- Kosmetyki
- Perfumy
- Suplementy
- Sprzęt medyczny

## Muzyka i hobby
- Mikrofon
- Gitara
- Keyboard
- Książki
- Gry planszowe

## Biuro i szkoła
- Papier
- Drukarka
- Artykuły piśmiennicze
- Plecak szkolny

