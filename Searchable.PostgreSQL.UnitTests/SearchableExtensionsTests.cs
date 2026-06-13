using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Shouldly;
using Searchable.PostgreSQL.Contracts;
using Searchable.PostgreSQL.Enums;

namespace Searchable.PostgreSQL.UnitTests;

public class SearchableExtensionsTests
{
    [Fact]
    public void CleanSearchTermForILike_ReturnsEmptyForBlankInput()
    {
        string result = SearchableExtensions.CleanSearchTermForILike("   ");

        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("value", "value")]
    [InlineData("50%_off", "50\\%\\_off")]
    [InlineData("\\name", "\\\\name")]
    public void CleanSearchTermForILike_EscapesExpectedCharacters(string input, string expected)
    {
        string result = SearchableExtensions.CleanSearchTermForILike(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void ProcessSearchTerm_ReturnsEmptyForBlankInput()
    {
        string result = SearchableExtensions.ProcessSearchTerm("   ");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ProcessSearchTerm_EscapesFullTextSpecialCharacters()
    {
        string result = SearchableExtensions.ProcessSearchTerm("alpha & beta | gamma");

        result.ShouldContain("\\&");
        result.ShouldContain("\\|");
    }

    [Fact]
    public void FullTextSearch_NullRequest_ReturnsOriginalQueryable()
    {
        using SearchableTestContext context = CreateContext();
        IQueryable<SamplePerson> query = context.People;

        IQueryable<SamplePerson> result = query.FullTextSearch(
            (ISearchableRequest)null!,
            person => person.SearchVector!);

        result.ShouldBeSameAs(query);
    }

    [Fact]
    public void FullTextSearch_BlankSearchTerm_ReturnsOriginalQueryable()
    {
        using SearchableTestContext context = CreateContext();
        IQueryable<SamplePerson> query = context.People;

        IQueryable<SamplePerson> result = query.FullTextSearch(
            new SearchableRequest("   "),
            person => person.SearchVector!);

        result.ShouldBeSameAs(query);
    }

    [Fact]
    public void FullTextSearch_WithTerm_ProducesExpectedSql()
    {
        using SearchableTestContext context = CreateContext();

        IQueryable<SamplePerson> query = context.People.FullTextSearch(
            new SearchableRequest("alpha beta"),
            person => person.SearchVector!,
            language: "english");

        string sql = query.ToQueryString().ToLowerInvariant();

        sql.ShouldContain("plainto_tsquery");
        sql.ShouldContain("@@");
        sql.ShouldContain("english");
    }

    [Fact]
    public void ILikeSearch_SingleProperty_WithTerm_ProducesExpectedSql()
    {
        using SearchableTestContext context = CreateContext();

        IQueryable<SamplePerson> query = context.People.ILikeSearch(
            new SearchableRequest("al"),
            person => person.FirstName!,
            ILikeMatchModeEnum.StartsWith);

        string sql = query.ToQueryString();

        sql.ShouldContain("ILIKE");
        sql.ShouldContain("al%");
    }

    [Fact]
    public void ILikeSearch_MultipleProperties_UseOrLogic_ProducesExpectedSql()
    {
        using SearchableTestContext context = CreateContext();

        IQueryable<SamplePerson> query = context.People.ILikeSearch(
            new SearchableRequest("al"),
            new Expression<Func<SamplePerson, string>>[]
            {
                person => person.FirstName!,
                person => person.LastName!
            },
            ILikeMatchModeEnum.Contains,
            useOrLogic: true);

        string sql = query.ToQueryString();

        sql.ShouldContain("ILIKE");
        sql.ShouldContain(" OR ");
    }

    [Fact]
    public void ILikeSearch_MultipleProperties_UseAndLogic_ProducesExpectedSql()
    {
        using SearchableTestContext context = CreateContext();

        IQueryable<SamplePerson> query = context.People.ILikeSearch(
            new SearchableRequest("al"),
            new Expression<Func<SamplePerson, string>>[]
            {
                person => person.FirstName!,
                person => person.LastName!
            },
            ILikeMatchModeEnum.Contains,
            useOrLogic: false);

        string sql = query.ToQueryString();

        sql.ShouldContain("ILIKE");
        sql.ShouldContain(" AND ");
    }

    private static SearchableTestContext CreateContext()
    {
        DbContextOptions<SearchableTestContext> options = new DbContextOptionsBuilder<SearchableTestContext>()
            .UseNpgsql("Host=localhost;Database=SearchableTests;Username=postgres;Password=postgres")
            .Options;

        return new SearchableTestContext(options);
    }

    private sealed record SearchableRequest(string? SearchTerm) : ISearchableRequest;

    private sealed class SearchableTestContext : DbContext
    {
        public SearchableTestContext(DbContextOptions<SearchableTestContext> options)
            : base(options)
        {
        }

        public DbSet<SamplePerson> People => Set<SamplePerson>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SamplePerson>(entity =>
            {
                entity.HasKey(person => person.Id);
                entity.Property(person => person.SearchVector).HasColumnType("tsvector");
            });
        }
    }

    private sealed class SamplePerson
    {
        public int Id { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public NpgsqlTsVector? SearchVector { get; set; }
    }
}
