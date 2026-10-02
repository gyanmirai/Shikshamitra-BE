using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Fee_selection_confirmation_should_hold_actual_onboarding_completion_even_when_finance_is_settled() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("FeeConfirm");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var structure = await CreateFeeSelectionReferenceAsync(fixture.Offering, cycle.AcademicYearId);
			var head = structure.FeeStructureDetails.Single().FeeHeadId;
			await WithUnitOfWorkAsync(async () => {
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				application.SaveFeeSelection(new(structure.Id, structure.Name, structure.RevisionNumber, cycle.AcademicYearId,
					fixture.Offering.StandardId, Guid.NewGuid(), "INR", [new(head, "QA Form", 200m, false, true, null, null)],
					TestUserId, DateTimeOffset.UtcNow), false);
				await _applicationRepository.UpdateAsync(application, true);
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Saved fee selection confirmation",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = false, RequireGuardianAccount = false });
			var fresh = await _applicationAppService.GetAsync(fixture.Application.Id);
			var finalized = await FinalizeAsync(fresh, true, "fee-confirm-finalize");
			var division = await WithUnitOfWorkAsync(async () => {
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "FeeConfirmA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "FEECONF", Name = "Saved fee selection", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "fee-confirm-activate", ConcurrencyStamp = item.ConcurrencyStamp });
			var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, finalized.StudentId, structure.Id, cycle.AcademicYearId);
			var due = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, head, (MonthEnum)5, 2026, false, 200m, new DateTime(2026, 6, 10));
			due.BindAssignment(assignment.Id); due.ApplyPayment(200m);
			assignment.CaptureAssignment("fee-confirm-qa", JsonSerializer.Serialize(new StudentFeeAssignmentPreviewDto {
				StudentId = finalized.StudentId, AcademicYearId = cycle.AcademicYearId, StructureId = structure.Id,
				RevisionNumber = structure.RevisionNumber, ProratedTotal = 200m,
				Heads = [new() { FeeHeadId = head, Included = true }], Lines = [new() { DueFeeId = due.Id }] }));
			var receipt = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, finalized.StudentId, cycle.AcademicYearId, 200, "QA-FEE-CONFIRM", FeeCollectionStatus.Paid);
			await WithUnitOfWorkAsync(async () => {
				await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true);
				await GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(due, true);
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(receipt, true);
				await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, receipt.Id, due.Id, 200), true);
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.FirstPaymentConfirmed.ShouldBe(true); item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			item.LastError.ShouldContain("Confirm the fee structure");
			var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
			var state = await fees.GetAsync(fixture.Application.Id); state.CanConfirmOnboarding.ShouldBeTrue();
			state = await fees.ConfirmOnboardingAsync(fixture.Application.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp });
			state.OnboardingConfirmed.ShouldBeTrue();
			item = await service.GetAsync(item.Id);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
			(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(assignment.Id)).AssignmentSnapshotJson.ShouldBe(assignment.AssignmentSnapshotJson);
		});
}
