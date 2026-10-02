using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact] public Task Record_return_same_year_reuses_enrollment_and_keeps_completed_renewal_and_finance_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: false, directStatus: true);
		var first = await fixture.Service.ActivateRenewalAsync(fixture.Renewal.Id, new() { ConcurrencyStamp = fixture.Renewal.ConcurrencyStamp, Remarks = "Annual owner checks completed" });
		var finance = await SeedRecordRenewalFinanceAsync(fixture.StudentId, first.AcademicYearId);
		await CollectRecordRenewalCashAsync(fixture.StudentId, first.AcademicYearId, finance.FeeHeadId, finance.DueId, 6000);
		first = await fixture.Service.GetRenewalAsync(first.Id);
		first = await fixture.Service.ActivateRenewalAsync(first.Id, new() { ConcurrencyStamp = first.ConcurrencyStamp, Remarks = "Confirmed annual Finance handoff" });
		first.Status.ShouldBe("Completed");
		var enrollment = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.StudentId && x.AcademicYearId == first.AcademicYearId));
		var feeId = (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(x => x.StudentId == fixture.StudentId && x.AcademicYearId == first.AcademicYearId))).Id;
		var differentClass = await WithUnitOfWorkAsync(async () =>
		{
			var old = await _standardRepository.GetAsync(first.StandardId);
			var standard = new Standard(Guid.NewGuid(), TestTenantId, "Other return class", 4, old.EducationMediumStageId, null); await _standardRepository.InsertAsync(standard, true);
			var division = new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "Other return A"); await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			return (standard.Id, Division: division.Id);
		});
		for (var attempt = 1; attempt <= 2; attempt++)
		{
			var exit = new StudentRecordTransitionBatchInput { AcademicYearId = first.AcademicYearId, IdempotencyKey = "same-year-exit-" + attempt, Items = [new() { StudentId = fixture.StudentId, Outcome = "Withdraw", Reason = "Reviewed temporary withdrawal", EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow) }] };
			var row = (await fixture.Service.PreviewAsync(exit)).Items.Single(); exit.Items[0].StudentConcurrencyStamp = row.StudentConcurrencyStamp; exit.Items[0].EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
			(await fixture.Service.SubmitBatchAsync(exit)).Items.Single().Status.ShouldBe("Applied");
			if (attempt == 1)
			{
				var invalid = await fixture.Service.PreviewReturnAsync(new() { StudentId = fixture.StudentId, Route = "ReuseProfile", TargetAcademicYearId = first.AcademicYearId, TargetStandardId = differentClass.Id, TargetDivisionId = differentClass.Division, RollNumber = first.RollNumber, Reason = "Different class needs owner fee confirmation", IdempotencyKey = "same-year-finance-impact-preview" });
				invalid.Ready.ShouldBeFalse(); invalid.Issues.ShouldContain(x => x.Code == "Students:Records:FinancialPlacementImpactRequired");
			}
			var command = new StudentRecordReturnInput { StudentId = fixture.StudentId, Route = "ReuseProfile", TargetAcademicYearId = first.AcademicYearId, TargetStandardId = first.StandardId, TargetDivisionId = first.DivisionId, RollNumber = first.RollNumber, Reason = "Reviewed return in the same school year", IdempotencyKey = "same-year-return-" + attempt };
			var preview = await fixture.Service.PreviewReturnAsync(command); preview.Ready.ShouldBeTrue(); command.StudentConcurrencyStamp = preview.StudentConcurrencyStamp; command.EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
			var result = await fixture.Service.ReturnAsync(command); result.Status.ShouldBe("AwaitingRenewal"); result.RenewalCaseId.ShouldNotBe(first.Id);
			var review = await fixture.Service.GetRenewalAsync(result.RenewalCaseId!.Value);
			review = await fixture.Service.ActivateRenewalAsync(review.Id, new() { ConcurrencyStamp = review.ConcurrencyStamp, Remarks = "Reviewed former student identity and current placement" });
			review.Status.ShouldBe("Provisioning"); review.Issues.ShouldBeEmpty();
			review = await fixture.Service.ActivateRenewalAsync(review.Id, new() { ConcurrencyStamp = review.ConcurrencyStamp, Remarks = "Existing Finance evidence remains confirmed" }); review.Status.ShouldBe("Completed");
			var current = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(enrollment.Id)); current.IsActive.ShouldBeTrue(); current.RollNumber.ShouldBe(first.RollNumber);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.StudentId))).ShouldBe(2);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(feeId))).Id.ShouldBe(feeId);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(finance.DueId))).OutstandingAmount.ShouldBe(0);
			(await fixture.Service.ReturnAsync(command)).TransitionId.ShouldBe(result.TransitionId);
		}
		(await fixture.Service.GetRenewalsAsync(new() { StudentId = fixture.StudentId, AcademicYearId = first.AcademicYearId })).TotalCount.ShouldBe(3);
		(await fixture.Service.GetRenewalAsync(first.Id)).Status.ShouldBe("Completed");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.StudentId))).GrNumber.ShouldBe("GR-RENEWAL-001");
	});
	[Fact] public Task Record_renewal_pending_checklist_can_be_cancelled_and_replaced_without_losing_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: false);
		var original = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().GetAsync(fixture.Renewal.Id));
		var transition = await fixture.Service.GetAsync(original.TransitionId!.Value); transition.CanCancel.ShouldBeTrue();
		(await fixture.Service.CancelAsync(transition.Id, new() { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "Wrong target division chosen before activation" })).Status.ShouldBe("Cancelled");
		(await fixture.Service.GetRenewalAsync(original.Id)).Status.ShouldBe("Cancelled");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.StudentId))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(fixture.SourceEnrollment))).IsActive.ShouldBeTrue();
		var next = new StudentRecordTransitionBatchInput { AcademicYearId = fixture.SourceYear, IdempotencyKey = "replacement-renewal", Items = [RollCommand(fixture.StudentId, original.AcademicYearId, original.StandardId, original.DivisionId, null, original.RollNumber)] };
		AcceptRollPreview(next, await fixture.Service.PreviewAsync(next));
		var replacement = (await fixture.Service.SubmitBatchAsync(next)).Items.Single(); replacement.Status.ShouldBe("AwaitingRenewal"); replacement.RenewalCaseId.ShouldNotBe(original.Id);
		(await fixture.Service.GetRenewalsAsync(new() { StudentId = fixture.StudentId })).TotalCount.ShouldBe(2);
	});
	[Fact] public Task Record_renewal_provisioning_cannot_be_cancelled_after_enrollment_activation() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: false);
		await fixture.Service.ActivateRenewalAsync(fixture.Renewal.Id, new() { ConcurrencyStamp = fixture.Renewal.ConcurrencyStamp, Remarks = "Reviewed activation" });
		var renewal = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().GetAsync(fixture.Renewal.Id));
		var transition = await fixture.Service.GetAsync(renewal.TransitionId!.Value); transition.CanCancel.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.CancelAsync(transition.Id, new() { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "Activation has owner dependencies" }))).Code.ShouldBe("Students:Records:InvalidRenewalState");
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Status.ShouldBe("Provisioning");
	});
}
