namespace Searchable.PostgreSQL.Enums;

/// <summary>
/// Defines the pattern matching modes for ILIKE searches
/// </summary>
public enum ILikeMatchModeEnum
{
    /// <summary>
    /// Matches if the search term is contained anywhere in the text (default)
    /// </summary>
    Contains,

    /// <summary>
    /// Matches if the text starts with the search term
    /// </summary>
    StartsWith,

    /// <summary>
    /// Matches if the text ends with the search term
    /// </summary>
    EndsWith,

    /// <summary>
    /// Matches if the text is exactly equal to the search term (case-insensitive)
    /// </summary>
    Exact
}
