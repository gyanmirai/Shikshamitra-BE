using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class StudentFeeAssignmentApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BaseUrl = "/api/student-fee-structure";

	// Enrollment is a synthetic prerequisite, not a call to the removed registration API.
	// Publication, preview, assignment and collection reads use real HTTP endpoints.
	private async Task<(AssignStudentFeeStructureDto Input, Guid[] Heads)> CreateFixtureAsync(bool optionalHeads = false)
	{
		var heads = new Guid[optionalHeads ? 3 : 1];
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		Guid studentId;
		Guid metadataId;
		var candidate = new StandardFeeStructureRequest { Name = $"Synthetic assignment {Guid.NewGuid():N}" };
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (services.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(
			new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString())], "Test"))))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var provider = services.ServiceProvider;
			var year = await provider.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			var account = new AccountHead(Guid.NewGuid(), tenantId, year.Id, candidate.Name, AccountHeadType.Income, null, null);
			await provider.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(account, autoSave: true);
			var income = new TransactionCategory(Guid.NewGuid(), tenantId, year.Id, TransactionType.Income,
				candidate.Name, null, [], true, $"I-{Guid.NewGuid():N}"[..16], account.Id, DateTimeOffset.UtcNow.AddDays(-1), null);
			await provider.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, autoSave: true);
			var category = new FeeCategory(Guid.NewGuid(), tenantId, year.Id, candidate.Name, null);
			await provider.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, autoSave: true);
			for (var i = 0; i < heads.Length; i++)
			{
				var head = new FeeHead(Guid.NewGuid(), tenantId, year.Id, category.Id, $"Synthetic head {i}", FeeHeadModule.Finance, true, null, income.Id);
				await provider.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, autoSave: true);
				heads[i] = head.Id;
			}
			var student = new Student(Guid.NewGuid(), tenantId, "Synthetic", null, "Assignment", year.Id,
				Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId,
				status: StudentLifeCycleStatusEnum.Enrolled);
			await provider.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
			studentId = student.Id;
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, year.Id, Factory.SeededBoardId,
				Factory.SeededMediumId, null, candidate.Name);
			await provider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			metadataId = metadata.Id;
			var term = new PaymentTerm(Guid.NewGuid(), tenantId, year.Id, Factory.SeededBoardId, candidate.Name,
				$"T-{Guid.NewGuid():N}"[..16], null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, InitialDemandStatus.Draft);
			var version = new PaymentTermVersion(Guid.NewGuid(), tenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
				HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, null, null);
			version.SetInstallments([new PaymentTermInstallment(Guid.NewGuid(), version.Id, 1, "Annual",
				year.StartDate, year.EndDate, year.StartDate, year.StartDate, 100m, true, false)]);
			version.Publish(year.StartDate, userId);
			term.AddVersion(version);
			await provider.GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, autoSave: true);
			var book = new ReceiptBook(Guid.NewGuid(), tenantId, year.Id, Factory.SeededBoardId, Factory.SeededMediumId,
				Factory.SeededMediumStageId, candidate.Name, $"B-{Guid.NewGuid():N}"[..16], "", 1, 99999, "");
			await provider.GetRequiredService<IRepository<ReceiptBook, Guid>>().InsertAsync(book, autoSave: true);
			candidate.ReceiptBookId = book.Id;
			var policy = await provider.GetRequiredService<ICollectionPolicyAppService>().CreateAsync(new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = year.Id, PolicyName = candidate.Name, PolicyCode = $"P-{Guid.NewGuid():N}"[..16],
				Status = "Active", EnableLateFee = false, AllowOnlineCollection = true,
				LateFeeType = "PerDay", PercentageBase = "OutstandingAmount", CompoundFrequency = "Daily", RoundingMethod = "None"
			});
			candidate.CollectionPolicyId = policy.Id;
			for (var i = 0; i < heads.Length; i++)
				candidate.FeeHeads.Add(new FeeHeadAssignmentRequest
				{
					FeeHeadId = heads[i], Amount = i == 0 ? 12_000m : i == 1 ? 6_000m : 2_000m,
					IsOptional = i > 0, IsOneTime = i == 2,
					PayableMonth = (MonthEnum)year.StartDate.Month, PayableYear = year.StartDate.Year,
					PaymentTermId = term.Id, PaymentTermVersionId = version.Id
				});
			await uow.CompleteAsync();
		}
		var draft = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures?feeStructureMetadataId={metadataId}",
			new StandardFeeStructureSaveRequest { StandardId = Factory.SeededStandardId, StandardFeeStructures = candidate });
		var published = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures/{draft.Id}/publish",
			new FeeStructurePublishRequest { ConcurrencyStamp = draft.ConcurrencyStamp });
		return (new AssignStudentFeeStructureDto { StudentId = studentId, AcademicYearId = Factory.SeededAcademicYearId,
			StandardFeeStructureId = published.Id }, heads);
	}

	private async Task<AssignmentCommitResultDto> PreviewAndAssignAsync(AssignStudentFeeStructureDto input)
	{
		var preview = await PostAndReadAsync<StudentFeeAssignmentPreviewDto>($"{BaseUrl}/preview", input);
		preview.PreviewToken.ShouldNotBeNullOrWhiteSpace();
		input.PreviewToken = preview.PreviewToken;
		var result = await PostAndReadAsync<AssignmentCommitResultDto>($"{BaseUrl}/assign", input);
		result.Outcome.ShouldBe("Assigned");
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var students = await services.ServiceProvider.GetRequiredService<IStudentRepository>().GetQueryableAsync();
		students.Where(student => student.Id == input.StudentId).Select(student => student.FeeStructureId).Single()
			.ShouldBe(input.StandardFeeStructureId, "The narrow tracked lookup must persist the student assignment link.");
		return result;
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Assignment_Root_Lookup_Is_Tracked_Tenant_Scoped_And_Does_Not_Load_Academic_Graph()
	{
		var (input, _) = await CreateFixtureAsync();
		using var services = Factory.Services.CreateScope();
		var provider = services.ServiceProvider;
		var tenant = provider.GetRequiredService<ICurrentTenant>();
		var uows = provider.GetRequiredService<IUnitOfWorkManager>();
		var students = provider.GetRequiredService<IStudentRepository>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uows.Begin(requiresNew: true))
		{
			var student = await students.GetForFeeAssignmentAsync(input.StudentId);
			student.ShouldNotBeNull();
			var db = await provider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.Entry(student).State.ShouldBe(EntityState.Unchanged);
			db.Entry(student).Reference(x => x.AcademicBoard).IsLoaded.ShouldBeFalse();
			db.Entry(student).Reference(x => x.Standard).IsLoaded.ShouldBeFalse();
		}
		using (tenant.Change(Guid.NewGuid()))
		using (var uow = uows.Begin(requiresNew: true))
			(await students.GetForFeeAssignmentAsync(input.StudentId)).ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Assign_Fee_Structure_And_Collect_Form_Shows_Ledger()
	{
		var (input, _) = await CreateFixtureAsync();
		await PreviewAndAssignAsync(input);
		var form = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		form.Ledger.ShouldNotBeEmpty();
		form.Summary.TotalFee.ShouldBe(12_000d);
		form.Summary.PendingFee.ShouldBe(12_000d);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reuse_Identical_Assignment_And_Reject_A_Changed_Retry()
	{
		var (input, _) = await CreateFixtureAsync();
		var first = await PreviewAndAssignAsync(input);
		var repeat = await PostAndReadAsync<AssignmentCommitResultDto>($"{BaseUrl}/assign", input);
		repeat.Outcome.ShouldBe("AlreadyAssigned");
		repeat.AssignmentId.ShouldBe(first.AssignmentId);
		input.OverrideReason = "Different synthetic reviewed command";
		var different = await Client.PostAsJsonAsync($"{BaseUrl}/assign", input, JsonOptions);
		// The deployed Finance business-error contract is 400 with an exact errorCode.
		different.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await different.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		problem.GetProperty("errorCode").GetString().ShouldBe(AssignmentErrorCodes.AlreadyAssigned);
		var form = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		form.Summary.TotalFee.ShouldBe(12_000d, "Retries must not create additional obligations.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Fee_Head_Summaries_After_Assignment()
	{
		var (input, _) = await CreateFixtureAsync();
		await PreviewAndAssignAsync(input);
		var details = await GetAndReadAsync<StudentFeeDetailsDto>($"/api/fee-collections/{input.StudentId}/fee-details?academicYearId={input.AcademicYearId}");
		details.FeeHeads.ShouldNotBeEmpty();
		details.FeeOverview.TotalFee.ShouldBe(12_000d);
		details.FeeOverview.PendingFee.ShouldBe(12_000d);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Student_Fee_Structure_Returns_Fee_Heads_After_Assignment()
	{
		var (input, heads) = await CreateFixtureAsync();
		await PreviewAndAssignAsync(input);
		var result = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		result.StandardFeeStructureId.ShouldBe(input.StandardFeeStructureId);
		result.FeeHeads.ShouldContain(x => x.FeeHeadId == heads[0]);
		result.ConcessionPolicyId.ShouldBeNull();
		result.Assignment.ShouldNotBeNull();
		result.Assignment.Lines.Sum(x => x.ProratedAmount).ShouldBe(12_000m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Assign_Custom_Optional_Annual_And_OneTime_Amounts()
	{
		var (input, heads) = await CreateFixtureAsync(optionalHeads: true);
		input.SelectedOptionalFeeHeadIds = [heads[1], heads[2]];
		input.OverrideReason = "Synthetic approved total amount overrides";
		input.FeeHeadDetails = [new() { FeeHeadId = heads[1], Amount = 1500m }, new() { FeeHeadId = heads[2], Amount = 2500m }];
		await PreviewAndAssignAsync(input);
		var result = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		result.FeeHeads.Single(x => x.FeeHeadId == heads[1]).SelectedAmount.ShouldBe(1500m);
		result.FeeHeads.Single(x => x.FeeHeadId == heads[2]).SelectedAmount.ShouldBe(2500m);
		result.Assignment.ShouldNotBeNull();
		result.Assignment.Lines.Where(x => x.FeeHeadId == heads[1]).Sum(x => x.ProratedAmount).ShouldBe(1500m);
		result.Assignment.Lines.Where(x => x.FeeHeadId == heads[2]).Sum(x => x.ProratedAmount).ShouldBe(2500m);
		var form = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		form.Summary.TotalFee.ShouldBe(16_000d, "Recurring overrides are annual totals, not unapproved per-month replacements.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Assignment_Without_Reviewed_Preview()
	{
		var (input, _) = await CreateFixtureAsync();
		var response = await Client.PostAsJsonAsync($"{BaseUrl}/assign", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("Finance:Assignment:");
	}
}
