# Searchable.PostgreSQL

[![NuGet Version](https://img.shields.io/nuget/v/MatinDeWet.Searchable.PostgreSQL)](https://www.nuget.org/packages/MatinDeWet.Searchable.PostgreSQL)
[![CI Status](https://img.shields.io/github/actions/workflow/status/MatinDeWet/Searchable.PostgreSQL/CI.yml?branch=master)](https://github.com/MatinDeWet/Searchable.PostgreSQL/actions/workflows/CI.yml)
[![Publish Status](https://img.shields.io/github/actions/workflow/status/MatinDeWet/Searchable.PostgreSQL/nuget-publish.yml?branch=master)](https://github.com/MatinDeWet/Searchable.PostgreSQL/actions/workflows/nuget-publish.yml)

PostgreSQL-specific dynamic search helpers for Entity Framework Core.

This package provides a focused API for building PostgreSQL-backed search filters over `IQueryable<T>` using expression-based property selectors for both full-text search and `ILIKE` pattern matching.

## Package

```bash
dotnet add package MatinDeWet.Searchable.PostgreSQL
```

## What It Does

- Builds dynamic search predicates from a request object.
- Supports PostgreSQL full-text search via `tsvector` + `plainto_tsquery`.
- Supports `ILIKE` modes: `Contains`, `StartsWith`, `EndsWith`, and `Exact`.
- Escapes wildcard characters for safe `ILIKE` usage.
- Supports searching a single property or multiple properties with `OR` / `AND` logic.

## Usages

### 1. Full-text search on a tsvector property

Use this when your entity has a mapped PostgreSQL `tsvector` property and you want to point directly to that property with an expression selector.

```csharp
using NpgsqlTypes;
using Searchable.PostgreSQL;
using Searchable.PostgreSQL.Contracts;

IQueryable<Person> query = dbContext.People;
ISearchableRequest request = new SearchableRequest("john manager");

query = query.FullTextSearch(
    request,
    person => person.SearchVector,
    language: "english");
```

The selector expression is translated server-side, so you get the same simple property-targeting style as the single-property `ILIKE` API.

### 2. ILIKE search on a single property

```csharp
using Searchable.PostgreSQL;
using Searchable.PostgreSQL.Enums;

query = query.ILikeSearch(
    request,
    person => person.Email!,
    ILikeMatchModeEnum.Contains);
```

### 3. ILIKE search across multiple properties

```csharp
query = query.ILikeSearch(
    request,
    [person => person.FirstName!, person => person.LastName!, person => person.Email!],
    ILikeMatchModeEnum.StartsWith,
    useOrLogic: true);
```

### 4. Match modes

`ILikeMatchModeEnum` controls the generated PostgreSQL pattern:

- `Contains`: `%term%`
- `StartsWith`: `term%`
- `EndsWith`: `%term`
- `Exact`: `term`

## API Surface

- `Searchable.PostgreSQL.SearchableExtensions`
- `Searchable.PostgreSQL.Contracts.ISearchableRequest`
- `Searchable.PostgreSQL.Enums.ILikeMatchModeEnum`

The package uses the GPL-3.0-only license.
