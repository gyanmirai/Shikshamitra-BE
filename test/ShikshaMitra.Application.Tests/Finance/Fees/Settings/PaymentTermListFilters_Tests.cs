using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermListFilters_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IPaymentTermAppService _service;
	private readonly IRepository<PaymentTerm, Guid> _terms;

	public PaymentTermListFilters_Tests()
	{
		_service = GetRequiredService<IPaymentTermAppService>();
		_terms = GetRequiredService<IRepository<PaymentTerm, Guid>>();
	}

	[Fact]
	public async Task Version_Greater_Than_One_Filters_Before_Counting_And_Paging_And_Clears()
	{
		var input = await SeedContextAsync();
		for (var i = 0; i < 9; i++) await AddTermAsync(input, $"Version one {i}");
		var revised = await AddTermAsync(input, "Revised", 2);
		input.Filters = """[{"column":"currentVersionNumber","type":"greaterThan","value":1}]""";
		input.MaxResultCount = 1;
		var first = await _service.GetListAsync(input);
		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(revised.Id);
		first.Items.Single().CurrentVersionNumber.ShouldBe(2);
		input.SkipCount = 1;
		var next = await _service.GetListAsync(input);
		next.TotalCount.ShouldBe(1);
		next.Items.ShouldBeEmpty();
		input.SkipCount = 0;
		input.MaxResultCount = 20;
		input.Filters = "[]";
		(await _service.GetListAsync(input)).TotalCount.ShouldBe(10);
		input.Filters = null;
		(await _service.GetListAsync(input)).Items.Count.ShouldBe(10);
	}

	[Theory]
	[InlineData("equals", "2", 1)]
	[InlineData("greaterThan", "1", 2)]
	[InlineData("lessThan", "2", 1)]
	[InlineData("between", "{\"min\":2,\"max\":3}", 2)]
	public async Task Numeric_Operators_Use_The_Displayed_Version(string operation, string value, int count)
	{
		var input = await SeedContextAsync();
		await AddTermAsync(input, "First", 1);
		await AddTermAsync(input, "Second", 2);
		await AddTermAsync(input, "Third", 3);
		input.Filters = $$"""[{"column":"currentVersionNumber","type":"{{operation}}","value":{{value}}}]""";
		(await _service.GetListAsync(input)).TotalCount.ShouldBe(count);
	}

	[Theory]
	[InlineData(false, 3)]
	[InlineData(true, 1)]
	public async Task Version_Fallback_Respects_The_Current_Pointer_Or_Latest_When_There_Is_No_Draft_Or_Published_Version(bool hasPointer, int expected)
	{
		var input = await SeedContextAsync();
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var term = new PaymentTerm(Guid.NewGuid(), tenantId, input.AcademicYearId, input.BoardId!.Value, "Historical", "HISTORICAL", null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnInstallmentStartDate, null, InitialDemandStatus.Published);
		foreach (var number in new[] { 1, 3 })
			term.AddVersion(new PaymentTermVersion(Guid.NewGuid(), tenantId, term.Id, number, PaymentTermVersionStatus.Superseded, HolidayShiftingMethod.None, DemandGenerationRule.OnInstallmentStartDate, null, null, null));
		if (hasPointer) term.SetCurrentVersion(term.Versions.First(v => v.VersionNumber == 1).Id);
		await _terms.InsertAsync(term, autoSave: true);
		input.Filters = $$"""[{"column":"currentVersionNumber","type":"equals","value":{{expected}}}]""";
		var result = await _service.GetListAsync(input);
		result.TotalCount.ShouldBe(1);
		result.Items.Single().CurrentVersionNumber.ShouldBe(expected);
	}

	[Theory]
	[InlineData("contains", "THLY", 2)]
	[InlineData("equals", "monthly", 1)]
	[InlineData("startsWith", "MONTH", 2)]
	[InlineData("endsWith", "PLUS", 1)]
	public async Task Name_Operators_Compare_Case_Insensitively(string operation, string value, int count)
	{
		var input = await SeedContextAsync();
		await AddTermAsync(input, "Monthly");
		await AddTermAsync(input, "Monthly Plus");
		await AddTermAsync(input, "Annual");
		input.Filters = $$"""[{"column":"name","type":"{{operation}}","value":"{{value}}"}]""";
		(await _service.GetListAsync(input)).TotalCount.ShouldBe(count);
	}

	[Theory]
	[InlineData("Draft")]
	[InlineData("Active")]
	[InlineData("Archived")]
	public async Task Status_Uses_Public_Lifecycle_Including_An_Active_Master_With_A_Draft(string status)
	{
		var input = await SeedContextAsync();
		await AddTermAsync(input, "Draft revision", 2);
		await AddTermAsync(input, "Published", 1, draft: false);
		await AddTermAsync(input, "Retired", 1, draft: false, archived: true);
		input.IncludeArchived = true;
		input.Filters = $$"""[{"column":"status","type":"equals","value":"{{status}}"}]""";
		var result = await _service.GetListAsync(input);
		result.TotalCount.ShouldBe(1);
		result.Items.Single().Status.ToString().ShouldBe(status);
		if (status == "Archived")
		{
			input.IncludeArchived = false;
			(await _service.GetListAsync(input)).TotalCount.ShouldBe(0);
		}
	}

	[Fact]
	public async Task Alternatives_Are_Ored_And_Columns_Intersect_With_Filtered_Totals_On_Subsequent_Pages()
	{
		var input = await SeedContextAsync();
		await AddTermAsync(input, "Monthly", 2);
		await AddTermAsync(input, "Quarterly", 3);
		await AddTermAsync(input, "Monthly old", 1);
		await AddTermAsync(input, "Annual", 2);
		input.Filters = """[{"column":"name","type":"equals","value":"Monthly"},{"column":"name","type":"equals","value":"Quarterly"},{"column":"currentVersionNumber","type":"greaterThan","value":1},{"column":"status","type":"equals","value":"Draft"}]""";
		input.MaxResultCount = 1;
		var first = await _service.GetListAsync(input);
		first.TotalCount.ShouldBe(2);
		input.SkipCount = 1;
		var second = await _service.GetListAsync(input);
		second.TotalCount.ShouldBe(2);
		second.Items.Single().Id.ShouldNotBe(first.Items.Single().Id);
		input.Filters = """[{"column":"name","type":"equals","value":"Missing"}]""";
		var missing = await _service.GetListAsync(input);
		missing.TotalCount.ShouldBe(0);
		missing.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Existing_Year_Board_Search_And_Master_Status_Still_Intersect()
	{
		var input = await SeedContextAsync();
		var match = await AddTermAsync(input, "Target", 2);
		await AddTermAsync(input, "Other", 2);
		var otherContext = await SeedContextAsync();
		await AddTermAsync(otherContext, "Target", 2);
		input.Search = match.Code;
		input.Status = PaymentTermStatus.Active;
		input.Filters = """[{"column":"currentVersionNumber","type":"greaterThan","value":1}]""";
		(await _service.GetListAsync(input)).Items.Single().Id.ShouldBe(match.Id);
		input.BoardId = otherContext.BoardId;
		(await _service.GetListAsync(input)).TotalCount.ShouldBe(0);
	}

	[Theory]
	[InlineData("not-json")]
	[InlineData("null")]
	[InlineData("[null]")]
	[InlineData("[{\"column\":null,\"type\":\"equals\",\"value\":\"x\"}]")]
	[InlineData("[{\"type\":\"equals\",\"value\":\"x\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":null,\"value\":\"x\"}]")]
	[InlineData("[{\"column\":\"name\",\"value\":\"x\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"equals\",\"value\":null}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"equals\"}]")]
	[InlineData("[{\"column\":\"currentVersionNumber\",\"type\":\"equals\",\"value\":null}]")]
	[InlineData("[{\"column\":\"currentVersionNumber\",\"type\":\"between\",\"value\":{\"min\":null,\"max\":2}}]")]
	[InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":null}]")]
	[InlineData("[{\"column\":\"actions\",\"type\":\"equals\",\"value\":1}]")]
	[InlineData("[{\"column\":\"currentVersionNumber\",\"type\":\"contains\",\"value\":2}]")]
	[InlineData("[{\"column\":\"currentVersionNumber\",\"type\":\"equals\",\"value\":\"bad\"}]")]
	[InlineData("[{\"column\":\"currentVersionNumber\",\"type\":\"between\",\"value\":{\"min\":3,\"max\":1}}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"equals\",\"value\":1}]")]
	[InlineData("[{\"column\":\"usage\",\"type\":\"greaterThan\",\"value\":1}]")]
	[InlineData("[{\"column\":\"status\",\"type\":\"contains\",\"value\":\"Active\"}]")]
	[InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":\"active\"}]")]
	[InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":\"Unknown\"}]")]
	[InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":1}]")]
	public async Task Invalid_Criteria_Are_Rejected_Even_Without_Matching_Rows(string filters)
	{
		var input = await SeedContextAsync();
		input.Filters = filters;
		await Should.ThrowAsync<AbpValidationException>(() => _service.GetListAsync(input));
	}

	[Fact]
	public async Task Usage_Filters_Count_Distinct_Heads_And_Classes_Within_The_Callers_Scope()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var input = await SeedContextAsync();
		var used = await AddTermAsync(input, "Used", 2);
		var unused = await AddTermAsync(input, "Unused");
		var medium = new EducationMedium(Guid.NewGuid(), tenantId, "English", input.BoardId!.Value);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, autoSave: true);
		var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Primary", null, medium.Id);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, autoSave: true);
		var visible = new Standard(Guid.NewGuid(), tenantId, "Visible class", 8, stage.Id, null);
		var hidden = new Standard(Guid.NewGuid(), tenantId, "Hidden class", 9, stage.Id, null);
		var standards = GetRequiredService<IRepository<Standard, Guid>>();
		await standards.InsertAsync(visible, autoSave: true);
		await standards.InsertAsync(hidden, autoSave: true);
		var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, input.AcademicYearId, input.BoardId.Value, medium.Id, null, "Usage filters");
		await GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
		var category = new FeeCategory(Guid.NewGuid(), tenantId, input.AcademicYearId, "Usage category", null);
		await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, autoSave: true);
		var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(GetRequiredService<IRepository<AccountHead, Guid>>(), GetRequiredService<IRepository<TransactionCategory, Guid>>(), input.AcademicYearId, tenantId, "Usage filters", DateTimeOffset.UtcNow);
		var heads = GetRequiredService<IRepository<FeeHead, Guid>>();
		var firstHead = new FeeHead(Guid.NewGuid(), tenantId, input.AcademicYearId, category.Id, "First", FeeHeadModule.Finance, true, null, incomeId);
		var hiddenHead = new FeeHead(Guid.NewGuid(), tenantId, input.AcademicYearId, category.Id, "Hidden", FeeHeadModule.Finance, true, null, incomeId);
		await heads.InsertAsync(firstHead, autoSave: true);
		await heads.InsertAsync(hiddenHead, autoSave: true);
		await AddUsageAsync(visible.Id, firstHead.Id);
		await AddUsageAsync(visible.Id, firstHead.Id); // Duplicate assignments must still display one head/class.
		await AddUsageAsync(hidden.Id, hiddenHead.Id);
		var deletedHead = new FeeHead(Guid.NewGuid(), tenantId, input.AcademicYearId, category.Id, "Deleted", FeeHeadModule.Finance, true, null, incomeId);
		await heads.InsertAsync(deletedHead, autoSave: true);
		await AddUsageAsync(visible.Id, deletedHead.Id);
		await heads.DeleteAsync(deletedHead, autoSave: true);
		var userId = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(userId, "usage-filter", "usage-filter@example.test", tenantId), autoSave: true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId, standardId: visible.Id), autoSave: true);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "Test")));
		foreach (var operation in new[] { "contains", "equals", "startsWith", "endsWith" })
		{
			var value = operation switch { "contains" => "HEADS · 1", "startsWith" => "1 fee", "endsWith" => "1 classes", _ => "1 fee heads · 1 classes" };
			input.Filters = $$"""[{"column":"usage","type":"{{operation}}","value":"{{value}}"},{"column":"currentVersionNumber","type":"greaterThan","value":1}]""";
			var result = await _service.GetListAsync(input);
			result.TotalCount.ShouldBe(1);
			result.Items.Single().Id.ShouldBe(used.Id);
			result.Items.Single().Usage!.FeeHeadCount.ShouldBe(1);
			result.Items.Single().Usage!.ClassCount.ShouldBe(1);
		}
		input.Filters = """[{"column":"usage","type":"equals","value":"0 fee heads · 0 classes"}]""";
		(await _service.GetListAsync(input)).Items.Single().Id.ShouldBe(unused.Id);

		async Task AddUsageAsync(Guid standardId, Guid headId)
		{
			var structureId = Guid.NewGuid();
			var structure = new FeeStructure(structureId, tenantId, metadata.Id, standardId, $"Usage {structureId:N}");
			var detail = new FeeStructureDetail(Guid.NewGuid(), tenantId, structure.Id, headId, 1000m, false, false, null, null);
			detail.AttachPaymentTerm(used.Id, used.Versions.First().Id);
			structure.FeeStructureDetails.Add(detail);
			await GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure, autoSave: true);
		}
	}

	private async Task<PaymentTermGetListInput> SeedContextAsync()
	{
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var start = new DateTime(2026, 1, 1);
		var end = new DateTime(2027, 12, 31);
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(yearId, tenantId, $"Filters {yearId:N}", start, end), autoSave: true);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new EducationBoard(boardId, tenantId, EducationBoardType.CBSE, yearId, start, end), autoSave: true);
		return new PaymentTermGetListInput { AcademicYearId = yearId, BoardId = boardId, MaxResultCount = 20 };
	}

	private async Task<PaymentTerm> AddTermAsync(PaymentTermGetListInput input, string name, int version = 1, bool draft = true, bool archived = false)
	{
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var id = Guid.NewGuid();
		var term = new PaymentTerm(id, tenantId, input.AcademicYearId, input.BoardId!.Value, name, id.ToString("N")[..20], null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnInstallmentStartDate, null, InitialDemandStatus.Published);
		if (version > 1) term.AddVersion(Version(1, PaymentTermVersionStatus.Published));
		term.AddVersion(Version(version, draft ? PaymentTermVersionStatus.Draft : PaymentTermVersionStatus.Published));
		if (archived) term.Archive("Retired test schedule");
		await _terms.InsertAsync(term, autoSave: true);
		return term;

		PaymentTermVersion Version(int number, PaymentTermVersionStatus status) => new(Guid.NewGuid(), tenantId, id, number, status, HolidayShiftingMethod.None, DemandGenerationRule.OnInstallmentStartDate, null, null, null);
	}
}
