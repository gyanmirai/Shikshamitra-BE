using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<FinalizedPlacementFixture> CreateFinalizedPlacementFixtureAsync(
		AdmissionOffering offering, Guid divisionId, bool activated, Guid? completedCaseDivisionId = null)
	{
		var applicant = await CreateApplicantAsync($"Placement {Guid.NewGuid():N}");
		var student = await CreateExistingStudentAsync(offering, "Placement", "Scope");
		var fixture = await WithUnitOfWorkAsync(async () =>
		{
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, $"PLACEMENT-{Guid.NewGuid():N}",
				offering.AdmissionCycleId, offering.Id, applicant.FirstName, "9000012612", null, AdmissionInquirySource.WalkIn, null);
			inquiry.LinkIdentity(applicant.FamilyId, applicant.Id);
			inquiry.SetStatus(AdmissionInquiryStatus.Converted);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
			var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"PLACEMENT-{Guid.NewGuid():N}",
				applicant.Id, inquiry.Id, offering.AdmissionCycleId, offering.Id,
				offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value);
			application.Submit(); application.Approve(TestUserId, "Scope fixture approval");
			await _applicationRepository.InsertAsync(application, true);
			var item = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, applicant.Id, student.Id,
				offering.Id, year, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
				$"placement-{application.Id:N}", null, DateTime.UtcNow.AddDays(3));
			await _onboardingCaseRepository.InsertAsync(item, true);
			if (activated)
			{
				var current = await _studentRepository.GetAsync(student.Id);
				current.UpdateAcademicPlacement(year, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, divisionId);
				current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
				await _studentRepository.UpdateAsync(current, true);
				var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, student.Id, year, offering.StandardId, divisionId);
				await _studentEnrollmentRepository.InsertAsync(enrollment, true);
				item.CompleteHistoricalImport(enrollment.Id, completedCaseDivisionId ?? divisionId, null, DateTime.UtcNow);
			}
			else
			{
				var seat = new StudentSeatReservation(Guid.NewGuid(), TestTenantId, item.Id, year, offering.StandardId,
					divisionId, 1, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30));
				await GetRequiredService<IRepository<StudentSeatReservation, Guid>>().InsertAsync(seat, true);
				item.RecordPlacement(divisionId, seat.Id, DateTime.UtcNow);
			}
			await _onboardingCaseRepository.UpdateAsync(item, true);
			application.FinalizeApprovedAdmission(student.Id, item.Id, DateTime.UtcNow);
			await _applicationRepository.UpdateAsync(application, true);
			var policy = HandoffPolicy(new AdmissionDocumentRequirement { Code = "SCOPE", Name = "Scope evidence" });
			var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), offering.WorkflowDefinitionId.Value,
				$"DOC-{application.Id:N}", "Scope documents", AdmissionWorkflowStageType.DocumentVerification, 1,
				true, false, null, JsonSerializer.Serialize(new { documentVerification = policy }));
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(definition, true);
			var workflow = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), TestTenantId, application.Id, offering.WorkflowDefinitionId.Value);
			workflow.Complete();
			await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().InsertAsync(workflow, true);
			var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), TestTenantId, workflow.Id, definition.Id,
				definition.Code, definition.Name, definition.StageType, 1, true);
			stage.Complete(TestUserId);
			await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(stage, true);
			return new DocumentHandoffFixture(new AdmissionApplicationDto { Id = application.Id }, offering, stage.Id, policy, student.Id);
		});
		var evidence = await AddHandoffEvidenceAsync(fixture, fixture.Policy.Requirements[0]);
		return new FinalizedPlacementFixture(fixture.Application.Id, applicant.Id, evidence.Id);
	}

	private sealed record FinalizedPlacementFixture(Guid ApplicationId, Guid ApplicantId, Guid EvidenceId);
}
