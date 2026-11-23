using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public Task Finance_verifies_joining_to_board_end_or_one_service_month(bool oneTime, bool prorated) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceBoardDates", oneTime: oneTime);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		var joined = year.StartDate.Date.AddMonths(2).AddDays(9);
		var boardEnd = year.EndDate.Date.AddMonths(-1);
		await WithUnitOfWorkAsync(async () =>
		{
			var boards = GetRequiredService<IRepository<EducationBoard, Guid>>();
			var board = await boards.GetAsync(fixture.Offering.BoardId);
			board.SetDates(year.StartDate, boardEnd);
			await boards.UpdateAsync(board, autoSave: true);
		});
		await SaveJoiningAnswerAsync(fixture, joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		if (prorated) await ConfigureProrationAsync(fixture);
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-board-dates");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var candidate = (await service.GetCandidatesAsync(finalized.StudentId, fixture.YearId)).Single();
		candidate.BoardEndDate?.Date.ShouldBe(boardEnd);
		var head = candidate.Heads.Single();
		head.IsOneTime.ShouldBe(oneTime);
		head.UsesSingleServiceMonth.ShouldBe(!oneTime && !prorated);
		var end = prorated ? boardEnd : joined.AddDays(DateTime.DaysInMonth(joined.Year, joined.Month) - joined.Day);
		var request = new AssignStudentFeeStructureDto
		{
			StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId,
			ServicePeriods = [new() { FeeHeadId = candidate.Heads.Single().FeeHeadId, Source = "Registration",
				SourceReferenceId = fixture.Application.Id, StartDate = joined, EndDate = end }]
		};
		var preview = await service.PreviewAsync(request);
		preview.Heads.Single().ServicePeriod!.StartDate?.Date.ShouldBe(joined);
		preview.Heads.Single().ServicePeriod!.EndDate?.Date.ShouldBe(end);
		var line = preview.Lines.Single();
		line.DueDate.Date.ShouldBe(year.StartDate.Date.AddDays(10));
		if (prorated) line.ProratedAmount.ShouldBeLessThan(1000m);
		else line.ProratedAmount.ShouldBe(1000m);
		if (!prorated)
		{
			request.ServicePeriods[0].Source = "ManualOverride";
			request.ServicePeriods[0].SourceReferenceId = null;
			request.ServicePeriods[0].EndDate = end.AddDays(1);
			(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(request))).Code.ShouldBe(AssignmentErrorCodes.OneTimeServiceMonth);
		}
		else
		{
			await SaveJoiningAnswerAsync(fixture, boardEnd.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			request.ServicePeriods.Clear();
			request.ChargePeriodSource = "BoardAcademicYear";
			(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(request))).Code.ShouldBe(ProrationPolicyErrorCodes.JoiningDateInvalid);
		}
	});

	[Fact]
	public Task Finance_initial_assignment_can_remove_an_optional_admission_fee_and_keeps_collection_in_sync() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceOptionalChoice", amount: 1000m, optionalFee: true);
		var optionalId = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<FeeStructure, Guid>>()
			.GetAsync(fixture.StructureId)).FeeStructureDetails.Single(h => h.IsOptional).FeeHeadId);
		var selections = GetRequiredService<IAdmissionFeeSelectionAppService>();
		var selection = await selections.GetAsync(fixture.Application.Id);
		await selections.SaveAsync(fixture.Application.Id, new() { StructureId = fixture.StructureId,
			OptionalHeadIds = [optionalId], ConcurrencyStamp = selection.ConcurrencyStamp });
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-optional-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var context = (await service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] })).Single();
		context.SelectedOptionalFeeHeadIds.ShouldContain(optionalId);
		var input = new AssignStudentFeeStructureDto { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId,
			StandardFeeStructureId = fixture.StructureId, SelectedOptionalFeeHeadIds = [optionalId] };
		(await service.PreviewAsync(input)).ProratedTotal.ShouldBe(1500m);
		input.SelectedOptionalFeeHeadIds = [];
		var preview = await service.PreviewAsync(input);
		preview.Heads.Single(h => h.FeeHeadId == optionalId).Included.ShouldBeFalse();
		preview.ProratedTotal.ShouldBe(1000m);
		input.PreviewToken = preview.PreviewToken;
		var committed = await service.AssignAsync(input);
		committed.Assignment.ProratedTotal.ShouldBe(1000m);
		(await service.AssignAsync(input)).AssignmentId.ShouldBe(committed.AssignmentId);
		var source = await GetRequiredService<IAdmissionFinanceContextReader>().GetAsync(fixture.Application.Id);
		source.OptionalHeadIds.ShouldBeEmpty();
		(await GetRequiredService<IAdmissionFeeCollectionAppService>().GetSummaryAsync(fixture.Application.Id)).TotalAmount.ShouldBe(1000m);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Finance_assignment_inherits_verified_admission_joining_date_and_default_proration(bool joinedBeforeYear) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceJoining", amount: 12000m, oneTime: false);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		var joined = year.StartDate.AddMonths(joinedBeforeYear ? -1 : 2).Date;
		await SaveJoiningAnswerAsync(fixture, joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		await ConfigureProrationAsync(fixture);
		var expected = await GetRequiredService<IAdmissionFeeCollectionAppService>().GetSummaryAsync(fixture.Application.Id);
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-joining-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var input = new AssignStudentFeeStructureDto { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId };
		var context = (await service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] })).Single();
		context.Status.ShouldBe("Selected");
		context.JoiningDate.ShouldBe(joined);
		context.StructureId.ShouldBe(fixture.StructureId);
		if (joinedBeforeYear)
		{
			context.ProrationPolicy.ShouldBeNull();
			context.AdmissionFeeHeadIds.ShouldBeEmpty();
		}
		var preview = await service.PreviewAsync(input);
		preview.RegistrationJoiningDate.ShouldBe(joined);
		preview.ProratedTotal.ShouldBe(expected.TotalAmount!.Value);
		if (joinedBeforeYear)
		{
			preview.ProrationPolicyVersionId.ShouldBeNull();
			preview.ReductionTotal.ShouldBe(0m);
			preview.Heads.Single().ServicePeriod!.Source.ShouldBe("AcademicYear");
			preview.Heads.Single().ServicePeriod!.StartDate.ShouldBe(year.StartDate.Date);
		}
		else
		{
			preview.ProrationPolicyVersionId.ShouldNotBeNull();
			preview.ReductionTotal.ShouldBeGreaterThan(0m);
			preview.Heads.Single().ServicePeriod!.Source.ShouldBe("Registration");
			preview.Heads.Single().ServicePeriod!.StartDate.ShouldBe(joined);
		}
		preview.Lines.Single().DueDate.Date.ShouldBe(year.StartDate.AddDays(10).Date);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentId == finalized.StudentId))).ShouldBeEmpty();
		input.PreviewToken = preview.PreviewToken;
		var committed = await service.AssignAsync(input);
		(await service.AssignAsync(input)).AssignmentId.ShouldBe(committed.AssignmentId);
		var saved = await service.GetByStudentIdAsync(finalized.StudentId, fixture.YearId);
		saved.Assignment.ProratedTotal.ShouldBe(preview.ProratedTotal);
		saved.Assignment.RegistrationJoiningDate.ShouldBe(joined);
		(await service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] })).Single().Status.ShouldBe("Assigned");
	});

	[Fact]
	public Task Finance_assignment_blocks_missing_admission_date_when_proration_applies() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceMissingJoining", amount: 12000m, oneTime: false);
		await ConfigureProrationAsync(fixture);
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-missing-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var exception = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(new()
		{ StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId }));
		exception.Code.ShouldBe(ProrationPolicyErrorCodes.JoiningDateRequired);
	});

	[Fact]
	public Task Finance_does_not_preview_a_second_assignment_after_admission_handoff() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceAlreadyAssigned");
		var collection = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var prepared = await collection.PrepareAsync(fixture.Application.Id, new());
		var original = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		await collection.CollectAsync(fixture.Application.Id, AdmissionCashPayment(original, 100m, "finance-assigned-payment"));
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-assigned-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var exception = await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(new()
		{ StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId }));
		exception.Code.ShouldBe(AssignmentErrorCodes.AlreadyAssigned);
		var saved = await service.GetByStudentIdAsync(finalized.StudentId, fixture.YearId);
		saved.Assignment.Lines.Single().DueFeeId.ShouldBe(original.Id);
		saved.Assignment.Lines.Single().PaidAmount.ShouldBe(100m);
		(await AccountDuesAsync(prepared.AccountId.Value)).Count.ShouldBe(1);
	});

	[Fact]
	public Task Finance_handoff_reuses_prepared_admission_dues_payments_and_policy_without_recalculation() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceRepairHandoff", amount: 12000m, oneTime: false);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		await SaveJoiningAnswerAsync(fixture, year.StartDate.AddMonths(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		var policy = await ConfigureProrationAsync(fixture);
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-repair-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		// A legacy/interrupted handoff can have a valid prepared account but no student assignment.
		var source = await GetRequiredService<IAdmissionFinanceContextReader>().GetAsync(fixture.Application.Id);
		var account = new FinanceFeeAccount(Guid.NewGuid(), TestTenantId, source.ApplicationId, source.ApplicantId,
			source.AcademicYearId, source.BoardId, source.MediumId, source.StageId, source.StandardId, source.ApplicantName, studentId: finalized.StudentId);
		account.SetDivision(source.DivisionId);
		await WithUnitOfWorkAsync(async () =>
		{
			var prepared = await GetRequiredService<StudentFeeStructureAppService>().PrepareAdmissionAsync(source, account.Id, []);
			account.CaptureAssignment(prepared.Preview.StructureId, prepared.Preview.RevisionNumber, JsonSerializer.Serialize(prepared.Preview), prepared.Fingerprint);
			await GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(account, true);
			await GetRequiredService<IRepository<DueFee, Guid>>().InsertManyAsync(prepared.Dues, true);
		});
		var original = (await AccountDuesAsync(account.Id)).Single();
		var receipt = await GetRequiredService<IAdmissionFeeCollectionAppService>().CollectAsync(fixture.Application.Id,
			AdmissionCashPayment(original, 100m, "finance-repair-paid"));
		policy = await GetRequiredService<IProrationPolicyAppService>().GetAsync(policy.Id);
		await GetRequiredService<IProrationPolicyAppService>().UpdateAsync(policy.Id, new()
		{
			AcademicYearId = fixture.YearId, PolicyName = policy.PolicyName, PolicyCode = policy.PolicyCode, Status = "Active", IsDefault = true,
			ConcurrencyStamp = policy.ConcurrencyStamp, ChangeReason = "Future admissions use daily calculation",
			FeeHeadRules = [new() { FeeHeadId = original.FeeHeadId, RuleType = "Daily", ApplyOnAdmission = true, ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }]
		});
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var context = (await service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] })).Single();
		context.Status.ShouldBe("Prepared");
		context.PreparedAssignment!.Lines.Single().PaidAmount.ShouldBe(100m);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetListAsync(x => x.StudentId == finalized.StudentId))).ShouldBeEmpty();
		var input = new AssignStudentFeeStructureDto { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId };
		var preview = await service.PreviewAsync(input);
		preview.ProrationPolicyVersionId.ShouldBe(original.ProrationPolicyVersionId);
		preview.ProratedTotal.ShouldBe(original.OriginalAmount);
		input.PreviewToken = preview.PreviewToken;
		var result = await service.AssignAsync(input);
		(await service.AssignAsync(input)).AssignmentId.ShouldBe(result.AssignmentId);
		var due = (await AccountDuesAsync(account.Id)).Single();
		due.Id.ShouldBe(original.Id);
		due.StudentFeeStructureId.ShouldBe(result.AssignmentId);
		due.PaidAmount.ShouldBe(100m);
		due.OriginalAmount.ShouldBe(original.OriginalAmount);
		due.ProrationPolicyVersionId.ShouldBe(original.ProrationPolicyVersionId);
		(await GetRequiredService<IAdmissionFeeCollectionAppService>().GetSummaryAsync(fixture.Application.Id)).Receipts.Single().Id.ShouldBe(receipt.FeeReceiptId!.Value);
	});

	[Fact]
	public Task Finance_preview_verifies_admission_dates_and_rejects_changed_source_on_commit() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceVerifiedDates", amount: 12000m, oneTime: false);
		var year = await ReadAsync<AcademicYear>(fixture.YearId);
		var joined = year.StartDate.AddMonths(2).Date;
		await SaveJoiningAnswerAsync(fixture, joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		await ConfigureProrationAsync(fixture);
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-verified-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var input = new AssignStudentFeeStructureDto { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId };
		var preview = await service.PreviewAsync(input);
		input.ServicePeriods = [new() { FeeHeadId = preview.Heads.Single().FeeHeadId, Source = "Registration",
			SourceReferenceId = fixture.Application.Id, StartDate = joined.AddDays(1), EndDate = year.EndDate }];
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.SourceUnavailable);
		input.ServicePeriods = [];
		input.PreviewToken = preview.PreviewToken;
		await SaveJoiningAnswerAsync(fixture, joined.AddMonths(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == finalized.StudentId))).ShouldBeEmpty();
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<BusinessException>(() => service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] }));
		await Should.ThrowAsync<BusinessException>(() => service.GetContextsAsync(new() { AcademicYearId = Guid.NewGuid(), StudentIds = [finalized.StudentId] }));
	});

	[Theory]
	[InlineData("FullCharge", true)]
	[InlineData("Daily", false)]
	public Task Finance_full_charge_and_withdrawal_only_rules_do_not_require_a_joining_date(string model, bool applyOnAdmission) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceFullCharge", amount: 12000m, oneTime: false);
		var policy = await ConfigureProrationAsync(fixture);
		policy = await GetRequiredService<IProrationPolicyAppService>().GetAsync(policy.Id);
		await GetRequiredService<IProrationPolicyAppService>().UpdateAsync(policy.Id, new()
		{
			AcademicYearId = fixture.YearId, PolicyName = policy.PolicyName, PolicyCode = policy.PolicyCode, Status = "Active", IsDefault = true,
			ConcurrencyStamp = policy.ConcurrencyStamp, ChangeReason = "No admission reduction",
			FeeHeadRules = [new() { FeeHeadId = policy.FeeHeadRules.Single().FeeHeadId, RuleType = model,
				ApplyOnAdmission = applyOnAdmission, ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }]
		});
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-full-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		(await service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] })).Single().Status.ShouldBe("Selected");
		var preview = await service.PreviewAsync(new() { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId });
		preview.ProratedTotal.ShouldBe(12000m);
		preview.ReductionTotal.ShouldBe(0m);
		preview.Heads.Single().ServicePeriod!.Source.ShouldBe("AcademicYear");
	});
}
