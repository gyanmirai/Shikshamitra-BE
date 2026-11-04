using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Correction_updates_linked_admission_account_selection_and_assignment_together() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("CorrectionUnpaidAdmission");
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "correction-unpaid-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var input = new AssignStudentFeeStructureDto { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId };
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var assigned = await service.AssignAsync(input);
		var assignment = await ReadAsync<StudentFeeStructure>(assigned.AssignmentId);
		// This admission fixture seeds legacy published structures directly. Use its
		// established revision fixture too; HTTP correction cases exercise actual publication.
		var revision = await CreateCollectibleRevisionAsync(fixture, 1000m);
		input.StandardFeeStructureId = revision.Id;
		input.EditAssignmentId = assigned.AssignmentId;
		input.AssignmentConcurrencyStamp = (await service.GetByStudentIdAsync(finalized.StudentId, fixture.YearId)).ConcurrencyStamp;
		input.EditReason = "Correct the selected published revision";
		var preview = await service.PreviewAsync(input);
		input.PreviewToken = preview.PreviewToken;
		(await service.AssignAsync(input)).AssignmentId.ShouldBe(assigned.AssignmentId);
		var account = await ReadAsync<FinanceFeeAccount>(assignment.FinanceFeeAccountId!.Value);
		account.StructureId.ShouldBe(revision.Id);
		account.StructureRevision.ShouldBe(preview.RevisionNumber);
		account.StudentFeeStructureId.ShouldBe(assigned.AssignmentId);
		account.ExtraProperties.ShouldContainKey("AssignmentCorrectionHistory");
		(await ReadAsync<AdmissionApplication>(fixture.Application.Id)).GetFeeSelection()!.StructureId.ShouldBe(revision.Id);
		(await AccountDuesAsync(account.Id)).Sum(d => d.OriginalAmount).ShouldBe(preview.ProratedTotal);
		(await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Correction_blocks_historical_admission_stage_payment_even_when_reversed_or_deleted(bool deleted) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("CorrectionLegacyAdmissionPayment");
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "correction-legacy-finalize");
		await ActivateCollectibleOnboardingAsync(fixture, finalized);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var input = new AssignStudentFeeStructureDto { StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId };
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var assigned = await service.AssignAsync(input);
		input.EditAssignmentId = assigned.AssignmentId;
		input.AssignmentConcurrencyStamp = (await service.GetByStudentIdAsync(finalized.StudentId, fixture.YearId)).ConcurrencyStamp;
		input.EditReason = "Attempt to correct fees";
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var application = await ReadAsync<AdmissionApplication>(fixture.Application.Id);
		var originalSelection = application.GetFeeSelection();
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var payment = AdmissionPaymentTransaction.RecordStaffPayment(Guid.NewGuid(), TestTenantId, application.Id,
				originalSelection!.OwnerStageId, 1, 25m, "admission", "INR", "cash", null, null, null, "{}",
				"historical-correction-payment", actor, DateTime.UtcNow, false);
			payment.Reverse("Historic instrument returned", "historical-correction-reverse", actor, DateTime.UtcNow);
			var payments = GetRequiredService<IRepository<AdmissionPaymentTransaction, Guid>>();
			await payments.InsertAsync(payment, true);
			if (deleted) await payments.DeleteAsync(payment, true);
		});
		var context = (await service.GetContextsAsync(new() { AcademicYearId = fixture.YearId, StudentIds = [finalized.StudentId] })).Single();
		context.CanEditStructure.ShouldBeFalse();
		context.StructureEditBlocker.ShouldNotBeNull().ShouldContain("payment", Case.Insensitive);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StructureEditBlocked);
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StructureEditBlocked);
		JsonSerializer.Serialize((await ReadAsync<AdmissionApplication>(application.Id)).GetFeeSelection()).ShouldBe(JsonSerializer.Serialize(originalSelection));
	});
}
