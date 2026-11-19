using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionWorkflowColumnFilters_Tests
{
    [Fact]
    public async Task Filters_displayed_usage_counts_before_total_sort_and_paging()
    {
        var a = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "A", "A");
        var b = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "B", "B");
        var c = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "C", "C");
        var service = Service([c, a, b], [Offering(a), Offering(b), Offering(b)]);
        var result = await service.GetListAsync(new AdmissionWorkflowListInput
        {
            ColumnFilters = """[{"column":"offeringCount","type":"greaterThan","value":0,"valueType":"number"}]""",
            Sorting = "offeringCount desc", SkipCount = 1, MaxResultCount = 1
        });
        result.TotalCount.ShouldBe(2);
        result.Items.Single().Id.ShouldBe(a.Id);
        result.Items.Single().OfferingCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("statusLabel", "Archived")]
    [InlineData("statusLabel desc", "Published")]
    [InlineData("status", "Draft")]
    [InlineData("status desc", "Archived")]
    public async Task Sorts_status_by_rendered_label_before_paging(string sorting, string expected)
    {
        var draft = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Draft", "DRAFT");
        var published = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Published", "PUBLISHED");
        published.Publish(null);
        var archived = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Archived", "ARCHIVED");
        archived.Publish(null);
        archived.Archive();
        var result = await Service([draft, published, archived], []).GetListAsync(new AdmissionWorkflowListInput { Sorting = sorting, MaxResultCount = 1 });
        result.TotalCount.ShouldBe(3);
        result.Items.Single().Name.ShouldBe(expected);
    }

    [Fact]
    public void Combines_text_ranges_and_date_predicates_and_excludes_unpublished_null_dates()
    {
        var rows = new[] {
            new AdmissionWorkflowColumnFilterRow { Name = "Spring A", Version = 1, OfferingCount = 0, LastModificationTime = new DateTime(2026, 10, 26, 12, 0, 0), PublishedAt = null },
            new AdmissionWorkflowColumnFilterRow { Name = "Spring B", Version = 2, OfferingCount = 3, LastModificationTime = new DateTime(2026, 10, 26, 17, 0, 0), PublishedAt = new DateTime(2026, 10, 25, 15, 0, 0) },
            new AdmissionWorkflowColumnFilterRow { Name = "Autumn", Version = 3, OfferingCount = 4, LastModificationTime = new DateTime(2026, 10, 27) }
        }.AsQueryable();
        var filtered = AdmissionWorkflowColumnFilters.Apply(rows, """
            [{"column":"name","type":"startsWith","value":"SPRING","valueType":"string"},
             {"column":"version","type":"between","value":{"min":0,"max":2},"valueType":"number"},
             {"column":"offeringCount","type":"between","value":{"min":0,"max":3},"valueType":"number"},
             {"column":"lastModificationTime","type":"equals","value":"26-10-2026","valueType":"date","startOffsetMinutes":-330,"endOffsetMinutes":-330},
             {"column":"publishedAt","type":"before","value":"2026-10-26","valueType":"date","startOffsetMinutes":-330,"endOffsetMinutes":-330}]
            """);
        filtered.Single().Name.ShouldBe("Spring B");
    }

    [Fact]
    public void Matches_local_calendar_day_across_utc_midnight_and_dst_change()
    {
        var rows = new[]
        {
            new AdmissionWorkflowColumnFilterRow { Name = "Before", PublishedAt = new DateTime(2026, 10, 25, 18, 29, 59, DateTimeKind.Utc) },
            new AdmissionWorkflowColumnFilterRow { Name = "Start", PublishedAt = new DateTime(2026, 10, 25, 18, 30, 0, DateTimeKind.Utc) },
            new AdmissionWorkflowColumnFilterRow { Name = "Last", PublishedAt = new DateTime(2026, 10, 26, 18, 29, 59, DateTimeKind.Utc) },
            new AdmissionWorkflowColumnFilterRow { Name = "After", PublishedAt = new DateTime(2026, 10, 26, 18, 30, 0, DateTimeKind.Utc) }
        }.AsQueryable();
        var criteria = """[{"column":"publishedAt","type":"equals","value":"26-10-2026","valueType":"date","startOffsetMinutes":-330,"endOffsetMinutes":-330}]""";
        AdmissionWorkflowColumnFilters.Apply(rows, criteria).Select(row => row.Name).ToArray().ShouldBe(["Start", "Last"]);
        AdmissionWorkflowColumnFilters.Apply(rows, criteria.Replace("equals", "before")).Single().Name.ShouldBe("Before");
        AdmissionWorkflowColumnFilters.Apply(rows, criteria.Replace("equals", "after")).Single().Name.ShouldBe("After");
        var dst = new[] { new AdmissionWorkflowColumnFilterRow { PublishedAt = new DateTime(2026, 11, 2, 4, 30, 0, DateTimeKind.Utc) } }.AsQueryable();
        AdmissionWorkflowColumnFilters.Apply(dst, """[{"column":"publishedAt","type":"equals","value":"2026-11-01","valueType":"date","startOffsetMinutes":240,"endOffsetMinutes":300}]""").Count().ShouldBe(1);
    }

    [Theory]
    [InlineData("equals")]
    [InlineData("before")]
    [InlineData("after")]
    public void Correlated_count_and_timestamp_predicates_translate_to_postgres(string operation)
    {
        using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseNpgsql("Host=localhost;Database=translation_only").Options);
        var workflows = context.Set<AdmissionWorkflowDefinition>();
        var offerings = context.Set<AdmissionOffering>();
        var filtered = AdmissionWorkflowColumnFilters.Apply(workflows.Select(workflow => new AdmissionWorkflowColumnFilterRow
        { Id = workflow.Id, Name = workflow.Name, Version = workflow.Version, OfferingCount = offerings.Count(offering => offering.WorkflowDefinitionId == workflow.Id), LastModificationTime = workflow.LastModificationTime ?? workflow.CreationTime, PublishedAt = workflow.PublishedAt }),
            "[{\"column\":\"name\",\"type\":\"contains\",\"value\":\"Spring\",\"valueType\":\"string\"},{\"column\":\"offeringCount\",\"type\":\"greaterThan\",\"value\":0,\"valueType\":\"number\"},{\"column\":\"publishedAt\",\"type\":\"" + operation + "\",\"value\":\"26-10-2026\",\"valueType\":\"date\",\"startOffsetMinutes\":-330,\"endOffsetMinutes\":-330}]");
        var sql = workflows.Where(workflow => filtered.Any(row => row.Id == workflow.Id)).OrderBy(workflow => workflow.Id).Skip(1).Take(1).ToQueryString();
        sql.ShouldContain("EXISTS");
        sql.ShouldContain("count(*)");
        sql.ShouldContain("lower(");
        sql.ShouldContain("\"TenantId\"");
        sql.ShouldContain("\"IsDeleted\"");
        sql.ShouldContain("LIMIT");
        sql.ShouldContain("OFFSET");
    }

    private sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var workflow = modelBuilder.Entity<AdmissionWorkflowDefinition>();
            workflow.HasKey(row => row.Id);
            workflow.Ignore(row => row.ExtraProperties);
            workflow.HasQueryFilter(row => row.TenantId == new Guid("11111111-1111-1111-1111-111111111111") && !row.IsDeleted);
            var offering = modelBuilder.Entity<AdmissionOffering>();
            offering.HasKey(row => row.Id);
            offering.Ignore(row => row.ExtraProperties);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[null]")]
    [InlineData("""[{"column":"actions","type":"equals","value":1,"valueType":"number"}]""")]
    [InlineData("""[{"column":"offeringCount","type":"equals","value":-1,"valueType":"number"}]""")]
    [InlineData("""[{"column":"publishedAt","type":"equals","value":"2026-02-30","valueType":"date"}]""")]
    [InlineData("""[{"column":"publishedAt","type":"equals","value":"2026-10-26","valueType":"date"}]""")]
    [InlineData("""[{"column":"publishedAt","type":"equals","value":"2026-10-26","valueType":"date","startOffsetMinutes":841,"endOffsetMinutes":0}]""")]
    [InlineData("""[{"column":"publishedAt","type":"equals","value":"2026-10-26","valueType":"date","startOffsetMinutes":0,"endOffsetMinutes":600}]""")]
    public void Rejects_invalid_criteria(string filters) =>
        Should.Throw<BusinessException>(() => AdmissionWorkflowColumnFilters.Apply(Array.Empty<AdmissionWorkflowColumnFilterRow>().AsQueryable(), filters));

    private static AdmissionOffering Offering(AdmissionWorkflowDefinition workflow)
    {
        var offering = new AdmissionOffering(Guid.NewGuid(), null, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("N"), true, null, null);
        offering.ConfigureReferences(workflow.Id, null, null);
        return offering;
    }

    private static AdmissionWorkflowReadService Service(AdmissionWorkflowDefinition[] workflows, AdmissionOffering[] offerings)
    {
        var offeringRepository = Substitute.For<IRepository<AdmissionOffering, Guid>>();
        offeringRepository.GetQueryableAsync().Returns(offerings.AsQueryable());
        var workflowRepository = Substitute.For<IRepository<AdmissionWorkflowDefinition, Guid>>();
        workflowRepository.GetQueryableAsync().Returns(workflows.AsQueryable());
        var applicationRepository = Substitute.For<IRepository<AdmissionApplication, Guid>>();
        applicationRepository.GetQueryableAsync().Returns(Array.Empty<AdmissionApplication>().AsQueryable());
        var provider = new ServiceCollection().AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter([])).BuildServiceProvider();
        return new AdmissionWorkflowReadService(workflowRepository, offeringRepository,
            Substitute.For<IRepository<AdmissionCycle, Guid>>(), applicationRepository,
            Substitute.For<IRepository<AdmissionApplicationWorkflowInstance, Guid>>(),
            Substitute.For<IRepository<AdmissionApplicationStageInstance, Guid>>(), null!)
        { LazyServiceProvider = new AbpLazyServiceProvider(provider) };
    }
}
