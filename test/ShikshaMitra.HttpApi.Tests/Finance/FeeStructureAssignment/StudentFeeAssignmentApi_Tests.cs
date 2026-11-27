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
using ShikshaMitra.Finance.Fees.FeeCollection;
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
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public partial class StudentFeeAssignmentApi_Tests(FinanceReceiptApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BaseUrl = "/api/student-fee-structure";

	[Fact]
	public async Task Variable_Fee_Change_Preserves_Earlier_Dues_And_Reprices_Future_Collection()
	{
		var (input, heads) = await CreateAssignmentFixtureAsync(true, true, false, true, true);
		input.SelectedOptionalFeeHeadIds = [heads[2]];
		input.VariableFeeHeads = [new() { FeeHeadId = heads[2], Amount = 300m, IsMonthlyRate = true }];
		var original = await PreviewAndAssignAsync(input);
		var originalLines = original.Assignment.Lines.Where(line => line.FeeHeadId == heads[2]).OrderBy(line => line.Sequence).ToList();
		var now = Factory.Services.GetRequiredService<IClock>().Now.Date;
		var firstFuture = originalLines.First(line => line.CoverageStartDate >= now && line.CoverageStartDate!.Value.Day == 1);
		var retained = originalLines.Last(line => line.Sequence < firstFuture.Sequence);
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<DueFee, Guid>>();
			var due = await repository.GetAsync(retained.DueFeeId!.Value);
			due.ApplyPayment(100m);
			await repository.UpdateAsync(due, autoSave: false);
			await uow.CompleteAsync();
		}
		var detailsUrl = $"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}";
		var current = await GetAndReadAsync<StudentFeeStructureResponseDto>(detailsUrl);
		current.CanEditStructure.ShouldBeFalse();
		current.Assignment!.Heads.Single(head => head.FeeHeadId == heads[2]).CanChangeVariableFee.ShouldBeTrue();
		var route = $"{BaseUrl}/student/{input.StudentId}/variable-fee-change";
		// Keep one covered month at the new amount and end the service before the next month.
		var newEnd = firstFuture.CoverageEndDate!.Value;
		var change = new VariableFeeChangeInput(input.AcademicYearId, heads[2], Guid.NewGuid(),
			current.ConcurrencyStamp, DateOnly.FromDateTime(firstFuture.CoverageStartDate!.Value),
			DateOnly.FromDateTime(newEnd), 500m, "Change the future service rate and end date");
		var quote = await PostAndReadAsync<VariableFeeChangeResult>(route + "/preview", change);
		quote.PreviousAmount.ShouldBe(quote.PreviousInstallments.Count * 300m);
		quote.NewAmount.ShouldBe(500m);
		var reviewed = change with { PreviewToken = quote.PreviewToken };
		var saved = await PostAndReadAsync<VariableFeeChangeResult>(route, reviewed);
		var retry = await PostAndReadAsync<VariableFeeChangeResult>(route, reviewed);
		retry.NewInstallments.Select(line => line.DueFeeId).ShouldBe(saved.NewInstallments.Select(line => line.DueFeeId));
		var updated = await GetAndReadAsync<StudentFeeStructureResponseDto>(detailsUrl);
		var prior = updated.Assignment!.Lines.Single(line => line.DueFeeId == retained.DueFeeId);
		prior.ProratedAmount.ShouldBe(300m);
		prior.PaidAmount.ShouldBe(100m);
		updated.Assignment.Heads.Single(head => head.FeeHeadId == heads[2]).ServicePeriod!.EndDate!.Value.Date.ShouldBe(newEnd.Date);
		var ledger = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		var feeRows = ledger.Ledger.SelectMany(month => month.Items).Where(line => line.FeeHeadId == heads[2]).ToList();
		feeRows.Single(line => line.DueFeeId == retained.DueFeeId).Total.ShouldBe(300d);
		feeRows.Any(line => quote.PreviousInstallments.Any(old => old.DueFeeId == line.DueFeeId)).ShouldBeFalse();
		foreach (var replacement in saved.NewInstallments)
			feeRows.Single(line => line.DueFeeId == replacement.DueFeeId).Total.ShouldBe(500d);
		var history = await GetAndReadAsync<AssignmentHistoryPage>($"{BaseUrl}/student/{input.StudentId}/history?academicYearId={input.AcademicYearId}");
		history.Items.Count(entry => entry.Action == "Variable fee changed").ShouldBe(1);
	}

	[Theory]
	[InlineData("Finance.Fees.StructureAssign.View")]
	[InlineData("Finance.Fees.StructureAssign.Add")]
	public async Task Should_Deny_Policy_Edit_And_Options_Without_Override_Permission(string permission)
	{
		using var restricted = Factory.CreateClient();
		restricted.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		restricted.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		var options = await restricted.GetAsync($"{BaseUrl}/policy-options?studentId={Guid.NewGuid()}&academicYearId={Factory.SeededAcademicYearId}");
		options.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var edit = await restricted.PutAsJsonAsync($"{BaseUrl}/student/{Guid.NewGuid()}/policies", new AssignmentPolicyUpdateDto {
			AcademicYearId = Factory.SeededAcademicYearId, Reason = "Synthetic policy edit", ConcurrencyStamp = "synthetic" });
		edit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Should_Edit_Assigned_Policies_With_Concurrency_And_Preserve_Obligations()
	{
		var (input, _) = await CreateFixtureAsync(useCurrentAcademicContext: true);
		var committed = await PreviewAndAssignAsync(input);
		var read = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		var options = await GetAndReadAsync<System.Collections.Generic.List<AssignmentPolicyDto>>($"{BaseUrl}/policy-options?studentId={input.StudentId}&academicYearId={input.AcademicYearId}");
		options.ShouldContain(p => p.Family == "collection");
		var update = new AssignmentPolicyUpdateDto { AcademicYearId = input.AcademicYearId, ConcurrencyStamp = read.ConcurrencyStamp, Reason = "Synthetic unchanged selection",
			Policies = new[] { "concession", "refund", "collection" }.Select(family => new AssignmentPolicySelectionDto {
				Family = family, VersionId = read.Assignment!.Policies.FirstOrDefault(p => p.Family == family)?.VersionId }).ToList() };
		var unsupported = new AssignmentPolicyUpdateDto { AcademicYearId = input.AcademicYearId, ConcurrencyStamp = read.ConcurrencyStamp,
			Reason = "Synthetic unsupported proration selection", Policies = update.Policies.Where(p => p.Family != "refund")
				.Append(new AssignmentPolicySelectionDto { Family = "proration" }).ToList() };
		unsupported.Policies.Count.ShouldBe(3, "The rejection must exercise the unsupported family, not the maximum count.");
		var rejected = await Client.PutAsJsonAsync($"{BaseUrl}/student/{input.StudentId}/policies", unsupported, JsonOptions);
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await rejected.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("errorCode").GetString()
			.ShouldBe(AssignmentErrorCodes.InvalidInput);
		var response = await Client.PutAsJsonAsync($"{BaseUrl}/student/{input.StudentId}/policies", update, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var saved = await response.Content.ReadFromJsonAsync<StudentFeeStructureResponseDto>(JsonOptions);
		saved!.StudentFeeStructureId.ShouldBe(committed.AssignmentId);
		saved.Assignment!.ProratedTotal.ShouldBe(committed.Assignment.ProratedTotal);
		saved.Assignment.Lines.Select(l => l.DueFeeId).ShouldBe(read.Assignment!.Lines.Select(l => l.DueFeeId));
		update.ConcurrencyStamp = "stale";
		var conflict = await Client.PutAsJsonAsync($"{BaseUrl}/student/{input.StudentId}/policies", update, JsonOptions);
		conflict.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	// Enrollment is a synthetic prerequisite, not a call to the removed registration API.
	// Publication, preview, assignment and collection reads use real HTTP endpoints.
	private Task<(AssignStudentFeeStructureDto Input, Guid[] Heads)> CreateFixtureAsync(bool optionalHeads = false,
		bool useCurrentAcademicContext = false, bool withRefundPolicy = false, bool variableOptionalHead = false)
		=> CreateAssignmentFixtureAsync(optionalHeads, useCurrentAcademicContext, withRefundPolicy, variableOptionalHead, false);

	private async Task<(AssignStudentFeeStructureDto Input, Guid[] Heads)> CreateAssignmentFixtureAsync(bool optionalHeads,
		bool useCurrentAcademicContext, bool withRefundPolicy, bool variableOptionalHead, bool monthlySchedule)
	{
		var heads = new Guid[optionalHeads ? 3 : 1];
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		Guid studentId;
		Guid metadataId;
		var academicYearId = Factory.SeededAcademicYearId;
		var boardId = Factory.SeededBoardId;
		var mediumId = Factory.SeededMediumId;
		var stageId = Factory.SeededMediumStageId;
		var standardId = Factory.SeededStandardId;
		var candidate = new StandardFeeStructureRequest { Name = $"Assign-{Guid.NewGuid():N}" };
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (services.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(
			new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString())], "Test"))))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var provider = services.ServiceProvider;
			if (useCurrentAcademicContext)
			{
				// Policy options require an effective version; the shared fixture can pick a past/future year.
				var today = provider.GetRequiredService<IClock>().Now.Date;
				var db = await provider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var context = await (from academicYear in db.Set<AcademicYear>().AsNoTracking()
					join board in db.Set<EducationBoard>() on academicYear.Id equals board.AcademicYearId
					join medium in db.Set<EducationMedium>() on board.Id equals medium.EducationBoardId
					join stage in db.Set<EducationMediumStage>() on medium.Id equals stage.EducationMediumId
					join standard in db.Set<Standard>() on stage.Id equals standard.EducationMediumStageId
					where academicYear.StartDate <= today && academicYear.EndDate >= today
					orderby standard.Id
					select new { YearId = academicYear.Id, BoardId = board.Id, MediumId = medium.Id, StageId = stage.Id, StandardId = standard.Id })
					.FirstAsync();
				academicYearId = context.YearId; boardId = context.BoardId; mediumId = context.MediumId;
				stageId = context.StageId; standardId = context.StandardId;
			}
			var year = await provider.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(academicYearId);
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
				boardId, mediumId, stageId, standardId,
				status: StudentLifeCycleStatusEnum.Enrolled);
			await provider.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
			studentId = student.Id;
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, year.Id, boardId,
				mediumId, null, candidate.Name);
			await provider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			metadataId = metadata.Id;
			var term = new PaymentTerm(Guid.NewGuid(), tenantId, year.Id, boardId, candidate.Name,
				$"T-{Guid.NewGuid():N}"[..16], null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, InitialDemandStatus.Draft);
			var version = new PaymentTermVersion(Guid.NewGuid(), tenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
				HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, null, null);
			if (monthlySchedule)
			{
				var first = new DateTime(year.StartDate.Year, year.StartDate.Month, 1);
				var count = (year.EndDate.Year - first.Year) * 12 + year.EndDate.Month - first.Month + 1;
				var allocation = decimal.Floor(10000m / count) / 100m;
				version.SetInstallments(Enumerable.Range(0, count).Select(i =>
				{
					var month = first.AddMonths(i);
					var start = month < year.StartDate.Date ? year.StartDate.Date : month;
					var last = month.AddMonths(1).AddDays(-1);
					var end = last > year.EndDate.Date ? year.EndDate.Date : last;
					return new PaymentTermInstallment(Guid.NewGuid(), version.Id, i + 1, month.ToString("MMM yyyy"),
						start, end, start, end, i < count - 1 ? allocation : 100m - allocation * (count - 1), true, false);
				}).ToArray());
			}
			else
				version.SetInstallments([new PaymentTermInstallment(Guid.NewGuid(), version.Id, 1, "Annual",
					year.StartDate, year.EndDate, year.StartDate, year.StartDate, 100m, true, false)]);
			version.Publish(year.StartDate, userId);
			term.AddVersion(version);
			await provider.GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, autoSave: true);
			var book = new ReceiptBook(Guid.NewGuid(), tenantId, year.Id, boardId, mediumId,
				stageId, candidate.Name, $"B-{Guid.NewGuid():N}"[..16], "", 1, 99999, "");
			await provider.GetRequiredService<IRepository<ReceiptBook, Guid>>().InsertAsync(book, autoSave: true);
			candidate.ReceiptBookId = book.Id;
			var policy = await provider.GetRequiredService<ICollectionPolicyAppService>().CreateAsync(new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = year.Id, PolicyName = candidate.Name, PolicyCode = $"P-{Guid.NewGuid():N}"[..16],
				Status = "Active", EnableLateFee = false, AllowOnlineCollection = true,
				LateFeeType = "PerDay", PercentageBase = "OutstandingAmount", CompoundFrequency = "Daily", RoundingMethod = "None"
			});
			candidate.CollectionPolicyId = policy.Id;
			if (monthlySchedule)
			{
				var proration = await provider.GetRequiredService<IProrationPolicyAppService>().CreateAsync(new CreateUpdateProrationPolicyDto
				{
					AcademicYearId = year.Id,
					PolicyName = candidate.Name + " proration",
					PolicyCode = $"PR-{Guid.NewGuid():N}"[..16],
					Status = "Active",
					FeeHeadRules = heads.Select(headId => new ProrationPolicyRuleDto
					{
						FeeHeadId = headId,
						RuleType = "FullMonthCharge",
					}).ToList(),
				});
				candidate.ProrationPolicyId = proration.Id;
			}
			if (withRefundPolicy)
			{
				var expenseHead = new AccountHead(Guid.NewGuid(), tenantId, year.Id, candidate.Name + " Refund", AccountHeadType.Expense, null, null);
				await provider.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(expenseHead, true);
				var expense = new TransactionCategory(Guid.NewGuid(), tenantId, year.Id, TransactionType.Expense,
					candidate.Name + " Refund", null, [], true, $"E-{Guid.NewGuid():N}"[..16], expenseHead.Id, DateTimeOffset.UtcNow.AddDays(-1), null);
				await provider.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(expense, true);
				var refund = await provider.GetRequiredService<IRefundPolicyAppService>().CreateAsync(new CreateUpdateRefundPolicyDto {
					AcademicYearId = year.Id, PolicyName = candidate.Name + " Refund", PolicyCode = $"R-{Guid.NewGuid():N}"[..16],
					Status = "Active", FeeHeadScope = "AllFeeHeads", ExpenseCategoryId = expense.Id,
					AllowedPayoutMethods = ["Cash"], RoundingMethod = "None" });
				candidate.RefundPolicyId = refund.Id;
			}
			for (var i = 0; i < heads.Length; i++)
				candidate.FeeHeads.Add(new FeeHeadAssignmentRequest
				{
					FeeHeadId = heads[i], Amount = i == 2 && variableOptionalHead ? (decimal?)null : i == 0 ? 12_000m : i == 1 ? 6_000m : 2_000m,
					IsOptional = i > 0, IsOneTime = i == 2 && !variableOptionalHead, IsVariable = i == 2 && variableOptionalHead,
					PayableMonth = (MonthEnum)year.StartDate.Month, PayableYear = year.StartDate.Year,
					PaymentTermId = term.Id, PaymentTermVersionId = version.Id
				});
			await uow.CompleteAsync();
		}
		var draft = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures?feeStructureMetadataId={metadataId}",
			new StandardFeeStructureSaveRequest { StandardId = standardId, StandardFeeStructures = candidate });
		var published = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures/{draft.Id}/publish",
			new FeeStructurePublishRequest { ConcurrencyStamp = draft.ConcurrencyStamp });
		return (new AssignStudentFeeStructureDto { StudentId = studentId, AcademicYearId = academicYearId,
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
	public async Task Should_Reject_Fixed_Fee_Changes_And_Assign_Configured_Amounts()
	{
		var (input, heads) = await CreateFixtureAsync(optionalHeads: true);
		input.SelectedOptionalFeeHeadIds = [heads[1], heads[2]];
		input.FeeHeadDetails = [new() { FeeHeadId = heads[1], Amount = 1500m }, new() { FeeHeadId = heads[2], Amount = 2500m }];
		var changed = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
		changed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		input.FeeHeadDetails = [new() { FeeHeadId = heads[1], Amount = 6000m }, new() { FeeHeadId = heads[2], Amount = 2000m }];
		await PreviewAndAssignAsync(input);
		var result = await GetAndReadAsync<StudentFeeStructureResponseDto>($"{BaseUrl}/student/{input.StudentId}?academicYearId={input.AcademicYearId}");
		result.FeeHeads.Single(x => x.FeeHeadId == heads[1]).SelectedAmount.ShouldBe(6000m);
		result.FeeHeads.Single(x => x.FeeHeadId == heads[2]).SelectedAmount.ShouldBe(2000m);
		result.Assignment.ShouldNotBeNull();
		result.Assignment.Lines.Where(x => x.FeeHeadId == heads[1]).Sum(x => x.ProratedAmount).ShouldBe(6000m);
		result.Assignment.Lines.Where(x => x.FeeHeadId == heads[2]).Sum(x => x.ProratedAmount).ShouldBe(2000m);
		var form = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		form.Summary.TotalFee.ShouldBe(20_000d);
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
