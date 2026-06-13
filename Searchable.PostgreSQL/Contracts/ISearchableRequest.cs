namespace Searchable.PostgreSQL.Contracts;

/// <summary>
/// Represents a request that carries a raw search term.
/// </summary>
public interface ISearchableRequest
{
    /// <summary>
    /// Gets the raw search text used to build search predicates.
    /// </summary>
    string? SearchTerm { get; init; }
}
