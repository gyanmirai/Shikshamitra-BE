using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordMidnightSchedule_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Record_proration_real_withdrawal_replay_and_correction_preserve_paid_money_and_append_audit()
	{
		var fixture = await SeedAsync();
		using var actor = CreateClientAs(fixture.Actor); actor.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		await PrepareProrationRecordsAsync(fixture, actor, reviewer);
		var finance = await SeedWithdrawalFeesAsync(fixture);
		factory.UtcNow = DateTime.SpecifyKind(finance.Leaving.AddHours(8), DateTimeKind.Utc);
		var command = WithdrawalCommand(fixture, finance.Leaving);
		var preview = (await PostAsync<StudentRecordTransitionPreviewDto>(actor, Transitions + "/preview", command)).Items.Single();
		preview.Ready.ShouldBeTrue(string.Join("; ", preview.Issues.Select(x => x.Message)));
		preview.Proration.ShouldNotBeNull();
		preview.Proration.WithdrawalCredit.ShouldBe(finance.ExpectedCredit);
		preview.Proration.JoiningDate.Date.ShouldBe(finance.Joining.Date);
		preview.Proration.ExcessPaidAmount.ShouldBe(Math.Max(0m, finance.Paid - (finance.Original - finance.ExpectedCredit)));
		command.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp; command.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
		var item = (await PostAsync<StudentRecordTransitionBatchResultDto>(actor, Transitions + "/batches", command)).Items.Single();
		item.Status.ShouldBe("Applied", item.LastError);
		(await PostAsync<StudentRecordTransitionBatchResultDto>(actor, Transitions + "/batches", command)).Items.Single().Id.ShouldBe(item.Id);
		await InTenantAsync(async services =>
		{
			var due = await services.GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(finance.DueId);
			due.OriginalAmount.ShouldBe(finance.Original); due.ScheduledAmount.ShouldBe(12000m); due.PaidAmount.ShouldBe(finance.Paid);
			due.RefundedAmount.ShouldBe(0m); due.GetWithdrawalProrationCredit().ShouldBe(finance.ExpectedCredit);
			due.GetWithdrawalProrationSource().ShouldBe(item.Id);
			var events = await services.GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().GetListAsync(x => x.Id == item.Id);
			events.Count.ShouldBe(1); events[0].ActorUserId.ShouldBe(fixture.Actor); events[0].ContextJson.ShouldNotBeNull().ShouldContain("policyVersion");
			return true;
		});
		// The normal named correction asks Finance to reverse the linked non-cash credit atomically.
		var correction = await PostAsync<StudentRecordTransitionPreviewRowDto>(actor, $"{Transitions}/{item.Id}/correct", new StudentRecordTransitionCorrectionDto
		{ ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString("N"), Reason = "Synthetic departure was entered against the wrong effective date", PreviewOnly = false });
		correction.Ready.ShouldBeTrue(string.Join("; ", correction.Issues.Select(x => x.Message)));
		var appliedCorrection = await GetAsync<StudentRecordTransitionDto>(actor, $"{Transitions}/{correction.TransitionId}");
		appliedCorrection.Status.ShouldBe("Applied", appliedCorrection.LastError);
		await InTenantAsync(async services =>
		{
			var due = await services.GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(finance.DueId);
			due.GetWithdrawalProrationCredit().ShouldBe(0m); due.PaidAmount.ShouldBe(finance.Paid); due.OriginalAmount.ShouldBe(finance.Original);
			var audit = await services.GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().GetListAsync(x => x.StudentId == fixture.Student && x.Action == "withdrawal_proration_reversed");
			audit.Count.ShouldBe(1); audit[0].ContextJson.ShouldNotBeNull().ShouldContain(item.Id.ToString());
			return true;
		});
	}

	[StudentRecordsApplicationPostgresFact]
	public async Task Record_proration_denied_preview_and_transaction_rollback_do_not_change_the_ledger()
	{
		var fixture = await SeedAsync();
		using var actor = CreateClientAs(fixture.Actor); actor.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		await PrepareProrationRecordsAsync(fixture, actor, reviewer);
		var finance = await SeedWithdrawalFeesAsync(fixture);
		factory.UtcNow = DateTime.SpecifyKind(finance.Leaving.AddHours(8), DateTimeKind.Utc);
		// Header-restricted permissions prove Records authority alone cannot post a Finance adjustment.
		actor.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
			ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create,
			ShikshaMitraSubmodulePermissions.Students.Records_Transitions.View,
			ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));
		var preview = (await PostAsync<StudentRecordTransitionPreviewDto>(actor, Transitions + "/preview", WithdrawalCommand(fixture, finance.Leaving))).Items.Single();
		preview.Ready.ShouldBeFalse(); preview.Issues.ShouldContain(x => x.Message.Contains("recalculation permission"));
		actor.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		var pendingId = await InTenantAsync(async services =>
		{
			var pending = await services.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId,
				fixture.Student, fixture.Year, 100d, "PRO-PENDING-" + Guid.NewGuid().ToString("N")[..8], FeeCollectionStatus.InReconciliation), true);
			return pending.Id;
		});
		var pendingPreview = (await PostAsync<StudentRecordTransitionPreviewDto>(actor, Transitions + "/preview", WithdrawalCommand(fixture, finance.Leaving))).Items.Single();
		pendingPreview.Ready.ShouldBeFalse(); pendingPreview.Issues.ShouldContain(x => x.Message.Contains("pending payment"));
		await InTenantAsync(async services => { await services.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().DeleteAsync(pendingId, true); return true; });
		var sourceId = Guid.NewGuid();
		// Exercise a real PostgreSQL rollback with the same strict transaction boundary as approval.
		using (var serviceScope = factory.Services.CreateScope())
		{
			var services = serviceScope.ServiceProvider;
			using var tenant = services.GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(TenantId);
			using var principal = services.GetRequiredService<Volo.Abp.Security.Claims.ICurrentPrincipalAccessor>().Change(new System.Security.Claims.ClaimsPrincipal(
				new System.Security.Claims.ClaimsIdentity([new(Volo.Abp.Security.Claims.AbpClaimTypes.UserId, fixture.Actor.ToString()),
					new(Volo.Abp.Security.Claims.AbpClaimTypes.TenantId, TenantId.ToString()), new(Volo.Abp.Security.Claims.AbpClaimTypes.Role, "admin")], "SyntheticTest")));
			using var work = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			await services.GetRequiredService<WithdrawalProrationService>().ApplyAsync(sourceId, fixture.Student, fixture.Year, finance.Leaving, "Rollback probe");
			await work.SaveChangesAsync();
			await work.RollbackAsync();
		}
		await InTenantAsync(async services =>
		{
			var due = await services.GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(finance.DueId);
			due.GetWithdrawalProrationCredit().ShouldBe(0m); due.PaidAmount.ShouldBe(finance.Paid);
			(await services.GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().AnyAsync(x => x.Id == sourceId)).ShouldBeFalse();
			return true;
		});
	}

	[StudentRecordsApplicationPostgresFact]
	public async Task Record_proration_concurrent_owner_commands_commit_one_credit_and_safe_retry()
	{
		var fixture = await SeedAsync();
		using var actor = CreateClientAs(fixture.Actor); actor.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		await PrepareProrationRecordsAsync(fixture, actor, reviewer);
		var finance = await SeedWithdrawalFeesAsync(fixture);
		factory.UtcNow = DateTime.SpecifyKind(finance.Leaving.AddDays(-1).AddHours(8), DateTimeKind.Utc);
		var command = WithdrawalCommand(fixture, finance.Leaving);
		var preview = (await PostAsync<StudentRecordTransitionPreviewDto>(actor, Transitions + "/preview", command)).Items.Single();
		preview.Ready.ShouldBeTrue(string.Join("; ", preview.Issues.Select(x => x.Message)));
		command.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp; command.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
		var scheduled = (await PostAsync<StudentRecordTransitionBatchResultDto>(actor, Transitions + "/batches", command)).Items.Single();
		scheduled.Status.ShouldBe("Scheduled", scheduled.LastError);
		var sourceId = scheduled.Id;
		factory.UtcNow = DateTime.SpecifyKind(finance.Leaving.AddHours(8), DateTimeKind.Utc);
		factory.Barrier.Arm("UPDATE \"Finance_DueFees\"");
		var attempts = new[] { TickTransitionOnlyAsync(), TickTransitionOnlyAsync() };
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(60));
		factory.Barrier.Connections.Count.ShouldBe(2);
		factory.Barrier.ProviderFailures.Keys.Any(x => x is "40001" or "23505").ShouldBeTrue();
		await TickTransitionOnlyAsync();
		var applied = await GetAsync<StudentRecordTransitionDto>(actor, $"{Transitions}/{sourceId}");
		applied.Status.ShouldBe("Applied", applied.LastError); applied.LastError.ShouldBeNull();
		await InTenantAsync(async services =>
		{
			var due = await services.GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(finance.DueId);
			due.GetWithdrawalProrationCredit().ShouldBe(finance.ExpectedCredit); due.PaidAmount.ShouldBe(finance.Paid);
			(await services.GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().CountAsync(x => x.Id == sourceId)).ShouldBe(1);
			return true;
		});
	}

	private async Task PrepareProrationRecordsAsync(Fixture fixture, HttpClient actor, HttpClient reviewer)
	{
		var legacy = await PostAsync<StudentLegacyOverrideDto>(actor, $"/api/students/records/directory/{fixture.Student}/legacy-overrides", new RequestStudentLegacyOverrideDto { Reason = "Verified isolated proration test register" });
		await PostAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve", new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Synthetic source register reviewed" });
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct"; policy.Changes["Correction"].Mode = "Direct";
		policy.Transitions.OutcomeChecks["Withdraw"].RequireFinanceClearance = false;
		var settings = await GetAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings");
		using var save = await actor.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy, ConcurrencyStamp = settings.ConcurrencyStamp }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<ShikshaMitra.Common.Responses.Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings/publish", new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
	}

	private static StudentRecordTransitionBatchInput WithdrawalCommand(Fixture fixture, DateTime leaving) => new()
	{
		AcademicYearId = fixture.Year, IdempotencyKey = Guid.NewGuid().ToString("N"),
		Items = [new() { StudentId = fixture.Student, Outcome = "Withdraw", EffectiveDate = leaving, Reason = "Synthetic family relocation; actual final attendance date confirmed" }]
	};

	private Task<ProrationFinanceFixture> SeedWithdrawalFeesAsync(Fixture fixture) => InTenantAsync(async services =>
	{
		var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(fixture.Year);
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var joining = year.StartDate.AddMonths(2).Date; var leaving = year.StartDate.AddMonths(6).AddDays(-1).Date;
		var incomeHead = await services.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, fixture.Year, "Proration income " + suffix, AccountHeadType.Income, null, null), true);
		var income = await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, fixture.Year, TransactionType.Income, "Proration income " + Guid.NewGuid().ToString("N")[..8], null, [], true, "PRO-" + Guid.NewGuid().ToString("N")[..8], incomeHead.Id, DateTimeOffset.UtcNow.AddDays(-1), null), true);
		var category = await services.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, fixture.Year, "Proration test " + Guid.NewGuid(), null), true);
		var head = await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, fixture.Year, category.Id, "Recurring tuition " + suffix, FeeHeadModule.Finance, false, null, income.Id), true);
		var metadata = await services.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, fixture.Year, factory.SeededBoardId, factory.SeededMediumId, null, "Proration fixture"), true);
		var structure = await services.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, metadata.Id, factory.SeededStandardId, "Proration " + Guid.NewGuid()), true);
		var term = new PaymentTerm(Guid.NewGuid(), TenantId, fixture.Year, factory.SeededBoardId, "Proration annual " + suffix, "PRO-" + Guid.NewGuid().ToString("N")[..8], null, false, HolidayShiftingMethod.None, DemandGenerationRule.SpecificDate, null, InitialDemandStatus.Published);
		var termVersion = new PaymentTermVersion(Guid.NewGuid(), TenantId, term.Id, 1, PaymentTermVersionStatus.Draft, HolidayShiftingMethod.None, DemandGenerationRule.SpecificDate, null, null, null);
		var installment = new PaymentTermInstallment(Guid.NewGuid(), termVersion.Id, 1, "Annual tuition", year.StartDate, year.EndDate, year.StartDate, year.StartDate.AddDays(10), 100m, true, false, allowPartialOverride: true);
		termVersion.SetInstallments([installment]); termVersion.Publish(year.StartDate, fixture.Actor); term.AddVersion(termVersion);
		await services.GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, true);
		var detail = new FeeStructureDetail(Guid.NewGuid(), TenantId, structure.Id, head.Id, 12000m, false, false, null, null); detail.AttachPaymentTerm(term.Id, termVersion.Id); structure.AddDetail(detail); structure.Lock();
		await services.GetRequiredService<IRepository<FeeStructure, Guid>>().UpdateAsync(structure, true);
		var policy = new ProrationPolicy(Guid.NewGuid(), TenantId, fixture.Year, new("Proration fixture " + suffix, "PRO-" + Guid.NewGuid().ToString("N")[..8], null, ProrationPolicyStatus.Active, false,
			[new(Guid.NewGuid(), head.Id, ProrationRuleType.FullMonthCharge, ProrationRoundingMode.RoundUpRupee,
				CutoffDay: null, AfterCutoffAction: null, ApplyOnAdmission: true, ApplyOnEarlyWithdrawal: true,
				EarlyWithdrawalRuleType: ProrationRuleType.FullMonthCharge)]));
		await services.GetRequiredService<IRepository<ProrationPolicy, Guid>>().InsertAsync(policy, true);
		var version = await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, "proration", policy.Id, fixture.Year, policy.Version, policy.PolicyName, "Active", JsonSerializer.Serialize(policy.ToSnapshot()), "created"), true);
		var rule = policy.ToSnapshot().Rules.Single(); var calculator = new ProrationPolicyCalculationService();
		var admission = AssignmentAmountCalculator.Calculate(calculator, rule, 12000m, year.StartDate, year.EndDate, joining, year.EndDate);
		var withdrawal = AssignmentAmountCalculator.Calculate(calculator, rule, 12000m, year.StartDate, year.EndDate, joining, leaving);
		var due = new DueFee(Guid.NewGuid(), TenantId, fixture.Student, head.Id, MonthEnum.June, 2026, false, admission.ProratedAmount, year.StartDate.AddDays(10), detail.Id,
			term.Id, termVersion.Id, installment.Id, 1, "Annual tuition", year.StartDate, year.EndDate, year.StartDate, year.StartDate.AddDays(10), ScheduleSource.PaymentTerm,
			12000m, policy.Id, version.Id, joining, year.EndDate);
		due.PreserveAllocation(100m); var paid = Math.Min(6000m, admission.ProratedAmount); due.ApplyPayment(paid);
		var assignment = new StudentFeeStructure(Guid.NewGuid(), TenantId, fixture.Student, structure.Id, fixture.Year, null, null, ConcessionFeeHeadScope.EntireFeeStructure, [],
			prorationPolicyId: policy.Id, prorationPolicyVersionId: version.Id, chargeStartDate: joining, chargeEndDate: year.EndDate);
		assignment.CaptureAssignment(new string('a', 64), JsonSerializer.Serialize(new StudentFeeAssignmentPreviewDto
		{
			StudentId = fixture.Student, AcademicYearId = fixture.Year, StructureId = structure.Id, RegistrationJoiningDate = joining, ChargeStartDate = joining, ChargeEndDate = year.EndDate,
			ProrationPolicyId = policy.Id, ProrationPolicyVersionId = version.Id,
			Heads = [new() { FeeHeadId = head.Id, FeeHeadName = head.Name, Included = true }],
			Lines = [new() { DueFeeId = due.Id, FeeHeadId = head.Id }]
		}));
		await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true); due.BindAssignment(assignment.Id);
		await services.GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(due, true);
		return new ProrationFinanceFixture(due.Id, joining, leaving, admission.ProratedAmount, admission.ProratedAmount - withdrawal.ProratedAmount, paid);
	});
	private sealed record ProrationFinanceFixture(Guid DueId, DateTime Joining, DateTime Leaving, decimal Original, decimal ExpectedCredit, decimal Paid);
}
