using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Searchable.PostgreSQL.Contracts;
using Searchable.PostgreSQL.Enums;

namespace Searchable.PostgreSQL;

/// <summary>
/// Extension methods for building PostgreSQL-backed search queries.
/// </summary>
public static class SearchableExtensions
{
    /// <summary>
    /// Searches entities using a PostgreSQL tsvector property and full-text search.
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="queryable">The queryable to filter</param>
    /// <param name="request">The searchable request containing the search term</param>
    /// <param name="searchVectorProperty">Expression selecting the tsvector property</param>
    /// <param name="language">The language for text search (defaults to English)</param>
    /// <returns>Filtered queryable with entities matching the search term</returns>
    public static IQueryable<T> FullTextSearch<T>(
        this IQueryable<T> queryable,
        ISearchableRequest request,
        Expression<Func<T, NpgsqlTsVector>> searchVectorProperty,
        string language = "english")
        where T : class
    {
        if (request == null || searchVectorProperty == null)
        {
            return queryable;
        }

        string? searchTerm = request.SearchTerm;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return queryable;
        }

        // Validate language parameter
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be null, empty, or whitespace.", nameof(language));
        }

        // Process search term to handle multiple words and clean input
        string processedSearchTerm = ProcessSearchTerm(searchTerm);

        // Return early if processed term is empty after cleaning
        if (string.IsNullOrWhiteSpace(processedSearchTerm))
        {
            return queryable;
        }

        Expression<Func<T, bool>> predicate = BuildFullTextPredicate(searchVectorProperty, language, processedSearchTerm);
        return queryable.Where(predicate);
    }

    /// <summary>
    /// Searches entities using PostgreSQL ILIKE pattern matching for case-insensitive matches.
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="queryable">The queryable to filter</param>
    /// <param name="request">The searchable request containing the search term</param>
    /// <param name="searchProperty">Expression to select the property to search on</param>
    /// <param name="matchMode">The pattern matching mode (contains, starts with, ends with, exact)</param>
    /// <returns>Filtered queryable with entities matching the search pattern</returns>
    public static IQueryable<T> ILikeSearch<T>(
        this IQueryable<T> queryable,
        ISearchableRequest request,
        Expression<Func<T, string>> searchProperty,
        ILikeMatchModeEnum matchMode = ILikeMatchModeEnum.Contains)
        where T : class
    {
        if (request == null)
        {
            return queryable;
        }

        string? searchTerm = request.SearchTerm;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return queryable;
        }

        // Clean the search term for ILIKE usage
        string cleanedSearchTerm = CleanSearchTermForILike(searchTerm);

        if (string.IsNullOrWhiteSpace(cleanedSearchTerm))
        {
            return queryable;
        }

        // Apply pattern based on match mode
        string pattern = matchMode switch
        {
            ILikeMatchModeEnum.StartsWith => $"{cleanedSearchTerm}%",
            ILikeMatchModeEnum.EndsWith => $"%{cleanedSearchTerm}",
            ILikeMatchModeEnum.Exact => cleanedSearchTerm,
            ILikeMatchModeEnum.Contains => $"%{cleanedSearchTerm}%",
            _ => $"%{cleanedSearchTerm}%"
        };

        Expression<Func<T, bool>> predicate = BuildIlikePredicate(searchProperty, pattern);
        return queryable.Where(predicate);
    }

    /// <summary>
    /// Searches entities using PostgreSQL ILIKE pattern matching on multiple properties.
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="queryable">The queryable to filter</param>
    /// <param name="request">The searchable request containing the search term</param>
    /// <param name="searchProperties">Array of expressions to select properties to search on</param>
    /// <param name="matchMode">The pattern matching mode (contains, starts with, ends with, exact)</param>
    /// <param name="useOrLogic">If true, uses OR logic between properties; if false, uses AND logic</param>
    /// <returns>Filtered queryable with entities matching the search pattern</returns>
    public static IQueryable<T> ILikeSearch<T>(
        this IQueryable<T> queryable,
        ISearchableRequest request,
        Expression<Func<T, string>>[] searchProperties,
        ILikeMatchModeEnum matchMode = ILikeMatchModeEnum.Contains,
        bool useOrLogic = true)
        where T : class
    {
        if (request == null || searchProperties == null || searchProperties.Length == 0)
        {
            return queryable;
        }

        string? searchTerm = request.SearchTerm;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return queryable;
        }

        // Clean the search term for ILIKE usage
        string cleanedSearchTerm = CleanSearchTermForILike(searchTerm);

        if (string.IsNullOrWhiteSpace(cleanedSearchTerm))
        {
            return queryable;
        }

        // Apply pattern based on match mode
        string pattern = matchMode switch
        {
            ILikeMatchModeEnum.StartsWith => $"{cleanedSearchTerm}%",
            ILikeMatchModeEnum.EndsWith => $"%{cleanedSearchTerm}",
            ILikeMatchModeEnum.Exact => cleanedSearchTerm,
            ILikeMatchModeEnum.Contains => $"%{cleanedSearchTerm}%",
            _ => $"%{cleanedSearchTerm}%"
        };

        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        Expression? combinedExpression = null;

        foreach (Expression<Func<T, string>> searchProperty in searchProperties)
        {
            Expression searchPropertyExpression = ReplaceParameter(
                searchProperty.Body,
                searchProperty.Parameters[0],
                parameter);

            MethodCallExpression ilikeExpression = BuildIlikeCall(searchPropertyExpression, pattern);

            combinedExpression = combinedExpression == null
                ? ilikeExpression
                : useOrLogic
                    ? Expression.OrElse(combinedExpression, ilikeExpression)
                    : Expression.AndAlso(combinedExpression, ilikeExpression);
        }

        if (combinedExpression == null)
        {
            return queryable;
        }

        Expression<Func<T, bool>> predicate = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
        return queryable.Where(predicate);
    }

    private static MethodCallExpression BuildIlikeCall(Expression valueExpression, string pattern)
    {
        MemberExpression functionsExpression = Expression.Property(
            null,
            typeof(EF).GetProperty(nameof(EF.Functions))!);

        return Expression.Call(
            typeof(NpgsqlDbFunctionsExtensions),
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            Type.EmptyTypes,
            functionsExpression,
            valueExpression,
            Expression.Constant(pattern));
    }

    private static Expression<Func<T, bool>> BuildIlikePredicate<T>(Expression<Func<T, string>> searchProperty, string pattern)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        Expression searchPropertyExpression = ReplaceParameter(searchProperty.Body, searchProperty.Parameters[0], parameter);
        MethodCallExpression ilikeExpression = BuildIlikeCall(searchPropertyExpression, pattern);

        return Expression.Lambda<Func<T, bool>>(ilikeExpression, parameter);
    }

    private static Expression<Func<T, bool>> BuildFullTextPredicate<T>(
        Expression<Func<T, NpgsqlTsVector>> searchVectorProperty,
        string language,
        string processedSearchTerm)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        Expression searchVectorExpression = ReplaceParameter(searchVectorProperty.Body, searchVectorProperty.Parameters[0], parameter);

        Expression<Func<NpgsqlTsVector, bool>> template = vector =>
            vector.Matches(EF.Functions.PlainToTsQuery(language, processedSearchTerm));

        Expression body = ReplaceExpression(template.Body, template.Parameters[0], searchVectorExpression);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)!;
    }

    private static Expression ReplaceExpression(Expression expression, Expression source, Expression target)
    {
        return new ExpressionReplaceVisitor(source, target).Visit(expression)!;
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }

    private sealed class ExpressionReplaceVisitor : ExpressionVisitor
    {
        private readonly Expression _source;
        private readonly Expression _target;

        public ExpressionReplaceVisitor(Expression source, Expression target)
        {
            _source = source;
            _target = target;
        }

        public override Expression? Visit(Expression? node)
        {
            if (node == _source)
            {
                return _target;
            }

            return base.Visit(node);
        }
    }

    /// <summary>
    /// Cleans the search term for safe use with PostgreSQL ILIKE.
    /// </summary>
    /// <param name="searchTerm">The raw search term</param>
    /// <returns>A cleaned search term safe for ILIKE usage</returns>
    internal static string CleanSearchTermForILike(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return string.Empty;
        }

        // Trim whitespace
        searchTerm = searchTerm.Trim();

        if (string.IsNullOrEmpty(searchTerm))
        {
            return string.Empty;
        }

        // Escape ILIKE special characters: % and _ are wildcards in ILIKE
        searchTerm = searchTerm.Replace("\\", "\\\\"); // Escape backslashes first
        searchTerm = searchTerm.Replace("%", "\\%");   // Escape percent signs
        searchTerm = searchTerm.Replace("_", "\\_");   // Escape underscores

        // Limit length to prevent performance issues
        const int maxSearchTermLength = 1000;
        if (searchTerm.Length > maxSearchTermLength)
        {
            searchTerm = searchTerm[..maxSearchTermLength].TrimEnd();
        }

        return searchTerm;
    }

    /// <summary>
    /// Processes the search term to ensure proper formatting for PostgreSQL full-text search.
    /// </summary>
    /// <param name="searchTerm">The raw search term input</param>
    /// <returns>A processed search term safe for PostgreSQL full-text search</returns>
    internal static string ProcessSearchTerm(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return string.Empty;
        }

        // Trim whitespace
        searchTerm = searchTerm.Trim();

        // Handle empty string after trimming
        if (string.IsNullOrEmpty(searchTerm))
        {
            return string.Empty;
        }

        // Escape special PostgreSQL characters that could cause issues
        // These characters have special meaning in PostgreSQL full-text search
        char[] specialChars = ['&', '|', '!', '(', ')', '<', '>', ':', '*'];

        foreach (char specialChar in specialChars)
        {
            searchTerm = searchTerm.Replace(specialChar.ToString(), $"\\{specialChar}");
        }

        // Replace multiple consecutive spaces with single space
        while (searchTerm.Contains("  "))
        {
            searchTerm = searchTerm.Replace("  ", " ");
        }

        // Limit the length to prevent potential performance issues
        // PostgreSQL can handle long queries, but extremely long ones may cause issues
        const int maxSearchTermLength = 1000;
        if (searchTerm.Length > maxSearchTermLength)
        {
            searchTerm = searchTerm[..maxSearchTermLength].TrimEnd();
        }

        return searchTerm;
    }
}
