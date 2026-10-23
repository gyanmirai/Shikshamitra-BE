using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Proration_admission_to_onboarding_activation_preserves_one_policy_account_and_paid_ledger() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("ProrationHandoff", amount: 12000m, oneTime: false);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		await SaveJoiningAnswerAsync(fixture, year.StartDate.AddMonths(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		await ConfigureProrationAsync(fixture);
		var collection = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var prepared = await collection.PrepareAsync(fixture.Application.Id, new());
		var before = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		await collection.CollectAsync(fixture.Application.Id, AdmissionCashPayment(before, 100m, "proration-handoff"));
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "proration-finalize");
		var onboardingSummary = await collection.GetSummaryAsync(fixture.Application.Id);
		onboardingSummary.Proration!.PolicyVersion.ShouldBe(prepared.Proration!.PolicyVersion);
		onboardingSummary.ConfirmedPaidAmount.ShouldBe(100m);
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var assignment = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(x => x.StudentId == finalized.StudentId && x.AcademicYearId == fixture.YearId));
		assignment.ProrationPolicyVersionId.ShouldBe(before.ProrationPolicyVersionId);
		var after = (await AccountDuesAsync(prepared.AccountId.Value)).Single();
		after.Id.ShouldBe(before.Id); after.StudentFeeStructureId.ShouldBe(assignment.Id);
		after.OriginalAmount.ShouldBe(before.OriginalAmount); after.PaidAmount.ShouldBe(100m);
		var invalidWithdrawal = await WithUnitOfWorkAsync(() => GetRequiredService<StudentTransitionFinanceClearanceReader>()
			.GetAsync(finalized.StudentId, fixture.YearId, year.StartDate.AddMonths(1), requireClearance: false));
		invalidWithdrawal.Cleared.ShouldBeFalse(); invalidWithdrawal.Detail.ShouldContain("registration joining date");
		(await AccountDuesAsync(prepared.AccountId.Value)).Single().GetWithdrawalProrationCredit().ShouldBe(0m);
	});

	[Fact]
	public Task Proration_late_joining_uses_registration_and_freezes_default_version_without_duplicate_dues() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("LateJoining", amount: 12000m, oneTime: false);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		var joined = year.StartDate.AddMonths(2);
		await SaveJoiningAnswerAsync(fixture, joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		var policy = await ConfigureProrationAsync(fixture);
		var collection = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var preview = await collection.GetSummaryAsync(fixture.Application.Id);
		preview.AccountId.ShouldBeNull(); preview.Proration.ShouldNotBeNull();
		preview.Proration.JoiningDate.Date.ShouldBe(joined.Date);
		preview.Proration.PolicyName.ShouldBe(policy.PolicyName);
		preview.Proration.AdmissionReduction.ShouldBeGreaterThan(0m);
		var prepared = await collection.PrepareAsync(fixture.Application.Id, new());
		var original = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		original.ProrationChargeStartDate.ShouldBe(joined.Date);
		original.OriginalAmount.ShouldBe(preview.TotalAmount!.Value);
		original.ScheduledAmount.ShouldBe(12000m);
		// A later policy version must never rewrite already prepared collection evidence.
		policy = await GetRequiredService<IProrationPolicyAppService>().GetAsync(policy.Id);
		await GetRequiredService<IProrationPolicyAppService>().UpdateAsync(policy.Id, new()
		{
			AcademicYearId = fixture.YearId, PolicyName = policy.PolicyName, PolicyCode = policy.PolicyCode,
			Status = "Active", IsDefault = true, ConcurrencyStamp = policy.ConcurrencyStamp, ChangeReason = "Future intake uses daily calculation",
			FeeHeadRules = [new() { FeeHeadId = original.FeeHeadId, RuleType = "Daily", ApplyOnAdmission = true, ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }]
		});
		var replay = await collection.PrepareAsync(fixture.Application.Id, new());
		replay.AccountId.ShouldBe(prepared.AccountId);
		replay.Proration!.PolicyVersion.ShouldBe(prepared.Proration!.PolicyVersion);
		var saved = (await AccountDuesAsync(prepared.AccountId.Value)).Single();
		saved.Id.ShouldBe(original.Id); saved.ProrationPolicyVersionId.ShouldBe(original.ProrationPolicyVersionId);
		saved.OriginalAmount.ShouldBe(original.OriginalAmount);
	});

	[Fact]
	public Task Proration_missing_registration_date_blocks_collection_with_actionable_summary() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("MissingJoining", oneTime: false);
		await ConfigureProrationAsync(fixture);
		var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var summary = await service.GetSummaryAsync(fixture.Application.Id);
		summary.CanCollect.ShouldBeFalse(); summary.Blocker.ShouldContain("actual joining date"); summary.TotalAmount.ShouldBeNull();
		(await Should.ThrowAsync<BusinessException>(() => service.PrepareAsync(fixture.Application.Id, new()))).Code.ShouldBe(ProrationPolicyErrorCodes.JoiningDateRequired);
	});

	[Theory][InlineData(false)][InlineData(true)]
	public Task Proration_no_extra_result_for_regular_or_one_time_fees(bool oneTime) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("RegularJoining", oneTime: oneTime);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		await SaveJoiningAnswerAsync(fixture, (oneTime ? year.StartDate.AddMonths(2) : year.StartDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		await ConfigureProrationAsync(fixture);
		var summary = await GetRequiredService<IAdmissionFeeCollectionAppService>().PrepareAsync(fixture.Application.Id, new());
		summary.TotalAmount.ShouldBe(1000m); summary.Proration.ShouldBeNull();
	});

	[Fact]
	public Task Proration_paid_registration_date_change_cannot_reset_financial_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("JoiningChanged", oneTime: false);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		await SaveJoiningAnswerAsync(fixture, year.StartDate.AddMonths(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		await ConfigureProrationAsync(fixture);
		var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var prepared = await service.PrepareAsync(fixture.Application.Id, new());
		var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		await service.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 100m, "joining-change"));
		await SaveJoiningAnswerAsync(fixture, year.StartDate.AddMonths(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		(await Should.ThrowAsync<BusinessException>(() => service.GetSummaryAsync(fixture.Application.Id))).Code.ShouldBe(AdmissionFeeCollectionErrorCodes.SelectionChanged);
		await Should.ThrowAsync<BusinessException>(() => service.PrepareAsync(fixture.Application.Id, new()));
		var persisted = (await AccountDuesAsync(prepared.AccountId.Value)).Single();
		persisted.Id.ShouldBe(due.Id); persisted.PaidAmount.ShouldBe(100m); persisted.OriginalAmount.ShouldBe(due.OriginalAmount);
	});

	[Theory][InlineData("JOINING_DATE", "2026-08-01", true)][InlineData("DATE_OF_BIRTH", "2018-08-01", false)]
	public Task Proration_joining_reader_supports_only_explicit_legacy_code(string code, string value, bool expected) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("LegacyJoining");
		await SaveJoiningAnswerAsync(fixture, value, code, binding: false);
		var date = await WithUnitOfWorkAsync(async () => await GetRequiredService<AdmissionJoiningDateReader>().ReadAsync(await _applicationRepository.GetAsync(fixture.Application.Id)));
		date.HasValue.ShouldBe(expected);
	});

	[Fact]
	public Task Proration_invalid_registration_date_is_rejected_and_other_tenant_cannot_read_answers() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("InvalidJoining");
		await SaveJoiningAnswerAsync(fixture, "2026-99-01");
		var application = await ReadAsync<AdmissionApplication>(fixture.Application.Id);
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<AdmissionJoiningDateReader>().ReadAsync(application)))).Code.ShouldBe(ProrationPolicyErrorCodes.JoiningDateInvalid);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => GetRequiredService<AdmissionJoiningDateReader>().ReadAsync(application)));
	});

	private async Task<ProrationPolicyDto> ConfigureProrationAsync(CollectibleAdmissionFixture fixture)
	{
		var head = (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeStructureDetail, Guid>>().GetListAsync(x => x.FeeStructureId == fixture.StructureId))).Single();
		return await GetRequiredService<IProrationPolicyAppService>().CreateAsync(new()
		{
			AcademicYearId = fixture.YearId, PolicyName = "Registration and withdrawal", PolicyCode = $"PRO-{Guid.NewGuid():N}"[..20],
			Status = "Active", IsDefault = true,
			FeeHeadRules = [new() { FeeHeadId = head.FeeHeadId, RuleType = "FullMonthCharge", ApplyOnAdmission = true, ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "FullMonthCharge" }]
		});
	}

	private Task SaveJoiningAnswerAsync(CollectibleAdmissionFixture fixture, string value, string code = "ACTUAL_JOINING_DATE", bool binding = true) => WithUnitOfWorkAsync(async () =>
	{
		var application = await _applicationRepository.GetAsync(fixture.Application.Id);
		var field = await _formFieldRepository.FindAsync(x => x.FormDefinitionId == application.FormDefinitionId && x.Code == code);
		if (field == null)
		{
			var section = (await _formSectionRepository.GetListAsync(x => x.FormDefinitionId == application.FormDefinitionId)).FirstOrDefault();
			if (section == null) section = await _formSectionRepository.InsertAsync(new(Guid.NewGuid(), application.FormDefinitionId, "ACADEMIC", "Academic details", 90, null), true);
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), application.FormDefinitionId, section.Id, code, "Actual joining date", AdmissionFormFieldType.Date,
				binding ? AdmissionFormBindingType.System : AdmissionFormBindingType.Custom, binding ? "Academic.JoiningDate" : null, false, false, 90, null, null, null, null, null), true);
		}
		var instances = GetRequiredService<IRepository<AdmissionFormInstance, Guid>>();
		var instance = await instances.FindAsync(x => x.ApplicationId == application.Id && x.FormDefinitionId == application.FormDefinitionId);
		if (instance == null) instance = await instances.InsertAsync(new(Guid.NewGuid(), TestTenantId, application.Id, application.FormDefinitionId, 1), true);
		var answers = GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>();
		var answer = await answers.FindAsync(x => x.FormInstanceId == instance.Id && x.FieldCode == code);
		if (answer == null) await answers.InsertAsync(new(Guid.NewGuid(), TestTenantId, instance.Id, code, JsonSerializer.Serialize(value)), true);
		else { answer.SetValue(JsonSerializer.Serialize(value)); await answers.UpdateAsync(answer, true); }
	});
}
