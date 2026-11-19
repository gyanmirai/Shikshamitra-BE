using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Linq;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionFormColumnFilters_Tests
{
	[Fact]
	public async Task Filters_section_counts_before_total_and_paging()
	{
		var forms = new[] { Form("Empty", 1), Form("Two sections", 2), Form("Three sections", 3) };
		var sections = new[] { Section(forms[1]), Section(forms[1]), Section(forms[2]), Section(forms[2]), Section(forms[2]) };
		var input = new AdmissionFormListInput { SkipCount = 1, MaxResultCount = 1,
			ColumnFilters = """[{"column":"sectionCount","type":"greaterThan","value":0,"valueType":"number"}]""" };
		var result = await Service(forms, sections).GetListAsync(input);
		result.TotalCount.ShouldBe(2);
		result.Items.Single().Name.ShouldBe("Three sections");
		result.Items.Single().SectionCount.ShouldBe(3);
	}

	[Fact]
	public async Task Column_filters_intersect_existing_search_purpose_status_and_version()
	{
		var target = Form("Spring Registration", 2);
		var inquiry = new AdmissionFormDefinition(Guid.NewGuid(), null, "Spring Inquiry", "INQUIRY", AdmissionFormLayoutType.SinglePage,
			version: 2, purpose: AdmissionFormPurpose.Inquiry);
		var archived = Form("Spring Archived", 2);
		archived.Publish(null);
		archived.Archive();
		var result = await Service([Form("Autumn", 2), Form("Spring v1", 1), inquiry, archived, target], [])
			.GetListAsync(new AdmissionFormListInput { Search = "Spring", Status = AdmissionFormStatus.Draft,
				Purpose = AdmissionFormPurpose.Registration, Version = 2,
				ColumnFilters = """[{"column":"name","type":"startsWith","value":"SPRING","valueType":"string"}]""" });
		result.TotalCount.ShouldBe(1);
		result.Items.Single().Id.ShouldBe(target.Id);
		result.Items.Single().SectionCount.ShouldBe(0);
	}

	[Fact]
	public async Task Relational_list_preserves_repository_scope_and_uses_identical_section_scope_for_filters_and_display()
	{
		using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseSqlite("Data Source=:memory:").Options);
		await context.Database.OpenConnectionAsync();
		await context.Database.EnsureCreatedAsync();
		var tenantId = new Guid("11111111-1111-1111-1111-111111111111");
		var target = new AdmissionFormDefinition(Guid.NewGuid(), tenantId, "Spring", "SPRING", AdmissionFormLayoutType.SinglePage);
		var deleted = new AdmissionFormDefinition(Guid.NewGuid(), tenantId, "Deleted", "DELETED", AdmissionFormLayoutType.SinglePage) { IsDeleted = true };
		var otherTenant = new AdmissionFormDefinition(Guid.NewGuid(), Guid.NewGuid(), "Other school", "OTHER", AdmissionFormLayoutType.SinglePage);
		context.AddRange(target, deleted, otherTenant);
		context.AddRange(Section(target), Section(target), Section(deleted), Section(deleted), Section(otherTenant), Section(otherTenant),
			new AdmissionFormSectionDefinition(Guid.NewGuid(), target.Id, "HIDDEN", "Hidden", 0, null));
		await context.SaveChangesAsync();
		var sections = context.Set<AdmissionFormSectionDefinition>().Where(section => section.Name != "Hidden");
		var result = await Service(context.Set<AdmissionFormDefinition>(), sections,
			new AsyncQueryableExecuter([new EfCoreAsyncQueryableProvider()])).GetListAsync(new AdmissionFormListInput
			{ ColumnFilters = """[{"column":"sectionCount","type":"equals","value":2,"valueType":"number"}]""" });
		result.TotalCount.ShouldBe(1);
		result.Items.Single().Id.ShouldBe(target.Id);
		result.Items.Single().SectionCount.ShouldBe(2);
	}

	private static IQueryable<AdmissionFormColumnFilterRow> Rows => new[]
	{
		new AdmissionFormColumnFilterRow { Id = Guid.NewGuid(), Name = "Spring intake", Version = 0, SectionCount = 0 },
		new AdmissionFormColumnFilterRow { Id = Guid.NewGuid(), Name = "Spring special", Version = 2, SectionCount = 3 },
		new AdmissionFormColumnFilterRow { Id = Guid.NewGuid(), Name = "Autumn intake", Version = int.MaxValue, SectionCount = 5 }
	}.AsQueryable();

	[Theory]
	[InlineData("name", "contains", "SPRING", "string", 2)]
	[InlineData("name", "equals", "SPRING INTAKE", "string", 1)]
	[InlineData("name", "startsWith", "Spring", "string", 2)]
	[InlineData("name", "endsWith", "intake", "string", 2)]
	[InlineData("version", "equals", "0", "number", 1)]
	[InlineData("version", "greaterThan", "2", "number", 1)]
	[InlineData("version", "lessThan", "2", "number", 1)]
	[InlineData("version", "equals", "2147483647", "number", 1)]
	[InlineData("sectionCount", "equals", "0", "number", 1)]
	[InlineData("sectionCount", "equals", "3", "number", 1)]
	[InlineData("sectionCount", "greaterThan", "3", "number", 1)]
	[InlineData("sectionCount", "lessThan", "3", "number", 1)]
	public void Applies_supported_predicates(string column, string operation, string value, string valueType, int count)
	{
		var filters = JsonSerializer.Serialize(new[] { new { column, type = operation, value, valueType } });
		AdmissionFormColumnFilters.Apply(Rows, filters).Count().ShouldBe(count);
	}

	[Fact]
	public void Combines_inclusive_numeric_ranges_with_name_before_counting_and_paging()
	{
		var filtered = AdmissionFormColumnFilters.Apply(Rows, """
			[{"column":"name","type":"startsWith","value":"Spring","valueType":"string"},
			 {"column":"version","type":"between","value":{"min":0,"max":2},"valueType":"number"},
			 {"column":"sectionCount","type":"between","value":{"min":"0","max":"3"},"valueType":"number"}]
			""");
		filtered.Count().ShouldBe(2);
		filtered.OrderBy(row => row.Version).Skip(1).Take(1).Single().Name.ShouldBe("Spring special");
		AdmissionFormColumnFilters.Apply(Rows,
			"""[{"column":"version","type":"between","value":{"min":2147483647,"max":2147483647},"valueType":"number"}]""")
			.Single().Name.ShouldBe("Autumn intake");
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("[null]")]
	[InlineData("[{}]")]
	[InlineData("[{\"column\":5,\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"purpose\",\"type\":\"equals\",\"value\":1,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":0,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"actions\",\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"greaterThan\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"contains\",\"value\":\"  \",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"contains\",\"value\":5,\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":1.5,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":-1,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":2147483648,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":null,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":true,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":{},\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"version\",\"type\":\"equals\",\"value\":\"1.0\",\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"sectionCount\",\"type\":\"between\",\"value\":{\"min\":3,\"max\":2},\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"sectionCount\",\"type\":\"between\",\"value\":{\"min\":0},\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"sectionCount\",\"type\":\"equals\",\"value\":2,\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"sectionCount\",\"type\":\"contains\",\"value\":2,\"valueType\":\"number\"}]")]
	public void Rejects_malformed_or_unsupported_criteria(string filters) =>
		Should.Throw<BusinessException>(() => AdmissionFormColumnFilters.Apply(Rows, filters)).Code.ShouldBe("Students:Admissions:InvalidFormFilter");

	[Fact]
	public void Bounds_criteria_text_and_total_payload_and_allows_empty_filters()
	{
		AdmissionFormColumnFilters.Apply(Rows, null).Count().ShouldBe(3);
		AdmissionFormColumnFilters.Apply(Rows, " ").Count().ShouldBe(3);
		AdmissionFormColumnFilters.Apply(Rows, "[]").Count().ShouldBe(3);
		var criterion = "{\"column\":\"name\",\"type\":\"contains\",\"value\":\"Spring\",\"valueType\":\"string\"}";
		AdmissionFormColumnFilters.Apply(Rows, "[" + string.Join(",", Enumerable.Repeat(criterion, 20)) + "]").Count().ShouldBe(2);
		Should.Throw<BusinessException>(() => AdmissionFormColumnFilters.Apply(Rows, "[" + string.Join(",", Enumerable.Repeat(criterion, 21)) + "]"));
		AdmissionFormColumnFilters.Apply(Rows, "[]" + new string(' ', 11998)).Count().ShouldBe(3);
		Should.Throw<BusinessException>(() => AdmissionFormColumnFilters.Apply(Rows, "[]" + new string(' ', 11999)));
		Should.Throw<BusinessException>(() => AdmissionFormColumnFilters.Apply(Rows, new string(' ', 12001)));
		Should.Throw<BusinessException>(() => AdmissionFormColumnFilters.Apply(Rows,
			JsonSerializer.Serialize(new[] { new { column = "name", type = "contains", value = new string('x', 151), valueType = "string" } })));
	}

	[Fact]
	public async Task List_rejects_oversized_blank_filter_payload()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() => Service([Form("Spring", 1)], []).GetListAsync(
			new AdmissionFormListInput { ColumnFilters = new string(' ', 12001) }));
		exception.Code.ShouldBe("Students:Admissions:InvalidFormFilter");
	}

	[Theory]
	[InlineData("contains")]
	[InlineData("equals")]
	[InlineData("startsWith")]
	[InlineData("endsWith")]
	public void Name_and_correlated_numeric_predicates_translate_to_postgres_before_paging(string operation)
	{
		using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseNpgsql("Host=localhost;Database=translation_only").Options);
		var formQuery = context.Set<AdmissionFormDefinition>().Where(form => form.Purpose == AdmissionFormPurpose.Inquiry);
		var sectionQuery = context.Set<AdmissionFormSectionDefinition>();
		var criteria = "[{\"column\":\"name\",\"type\":\"" + operation + "\",\"value\":\"Spring\",\"valueType\":\"string\"}," +
			"""{"column":"version","type":"greaterThan","value":0,"valueType":"number"},{"column":"sectionCount","type":"between","value":{"min":0,"max":3},"valueType":"number"}]""";
		var filtered = AdmissionFormColumnFilters.Apply(formQuery.Select(form => new AdmissionFormColumnFilterRow
		{ Id = form.Id, Name = form.Name, Version = form.Version, SectionCount = sectionQuery.Count(section => section.FormDefinitionId == form.Id) }), criteria);
		var sql = formQuery.Where(form => filtered.Any(row => row.Id == form.Id)).OrderByDescending(form => form.CreationTime).Skip(1).Take(1).ToQueryString();
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
			var form = modelBuilder.Entity<AdmissionFormDefinition>();
			form.HasKey(row => row.Id);
			form.Ignore(row => row.ExtraProperties);
			form.HasQueryFilter(row => row.TenantId == new Guid("11111111-1111-1111-1111-111111111111") && !row.IsDeleted);
			modelBuilder.Entity<AdmissionFormSectionDefinition>().HasKey(row => row.Id);
		}
	}

	private static AdmissionFormDefinition Form(string name, int version) =>
		new(Guid.NewGuid(), null, name, name.Replace(" ", ""), AdmissionFormLayoutType.SinglePage, version: version);

	private static AdmissionFormSectionDefinition Section(AdmissionFormDefinition form) =>
		new(Guid.NewGuid(), form.Id, Guid.NewGuid().ToString("N"), "Section", 0, null);

	private static AdmissionFormAppService Service(AdmissionFormDefinition[] forms, AdmissionFormSectionDefinition[] sections) =>
		Service(forms.AsQueryable(), sections.AsQueryable(), new AsyncQueryableExecuter([]));

	private static AdmissionFormAppService Service(IQueryable<AdmissionFormDefinition> forms, IQueryable<AdmissionFormSectionDefinition> sections, IAsyncQueryableExecuter executer)
	{
		var formRepository = Substitute.For<IRepository<AdmissionFormDefinition, Guid>>();
		formRepository.GetQueryableAsync().Returns(forms);
		var sectionRepository = Substitute.For<IRepository<AdmissionFormSectionDefinition, Guid>>();
		sectionRepository.GetQueryableAsync().Returns(sections);
		var provider = new ServiceCollection().AddSingleton(executer).BuildServiceProvider();
		return new(formRepository, sectionRepository,
			Substitute.For<IRepository<AdmissionFormFieldDefinition, Guid>>(), Substitute.For<IRepository<AdmissionOffering, Guid>>(),
			Substitute.For<IRepository<AdmissionApplication, Guid>>(), Substitute.For<IRepository<AdmissionPublicInquiryLink, Guid>>(), null!)
		{ LazyServiceProvider = new AbpLazyServiceProvider(provider) };
	}
}
