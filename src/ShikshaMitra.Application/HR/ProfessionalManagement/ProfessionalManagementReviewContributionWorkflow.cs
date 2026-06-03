using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementReviewContributionWorkflow(
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<ClassroomObservation, Guid> observationRepository,
	IRepository<PerformanceEvidence, Guid> evidenceRepository,
	IRepository<PerformanceFeedback, Guid> feedbackRepository,
	ProfessionalManagementReviewReader reviewReader,
	PerformanceAuditEventManager auditEventManager,
	IGuidGenerator guidGenerator,
	IClock clock)
	: ApplicationService, ITransientDependency
{
	public async Task<ClassroomObservationDto> AddObservationAsync(Guid id, ClassroomObservationRequest input)
	{
		(await assignmentRepository.GetAsync(id)).EnsureEditable();
		var review = await reviewReader.GetReviewAsync(id);
		if (review.Template?.StaffCategory is not (PerformanceStaffCategory.Teacher or PerformanceStaffCategory.ClassTeacher or PerformanceStaffCategory.HodCoordinator))
		{
			throw new BusinessException(HRErrorCodes.PerformanceObservationTeacherOnly);
		}

		var observationDate = input.ObservationDate == default
			? clock.Now
			: input.ObservationDate;
		var observation = new ClassroomObservation(guidGenerator.Create(), CurrentTenant.Id, id, input.ObserverEmployeeId, observationDate, input.ClassName, input.SubjectName, input.LessonObjectiveScore, input.StudentEngagementScore, input.ClassroomManagementScore, input.AssessmentScore, input.Notes);
		await observationRepository.InsertAsync(observation, autoSave: true);
		await AuditAsync(id, "ObservationAdded", "Added classroom observation");
		return ProfessionalManagementProjection.ToObservationDto(observation);
	}

	public async Task<PerformanceEvidenceDto> AddEvidenceAsync(Guid id, PerformanceEvidenceRequest input)
	{
		(await assignmentRepository.GetAsync(id)).EnsureEditable();
		var evidence = new PerformanceEvidence(guidGenerator.Create(), CurrentTenant.Id, id, input.CriterionId, input.Title, input.FileName, input.Url, input.Notes);
		await evidenceRepository.InsertAsync(evidence, autoSave: true);
		await AuditAsync(id, "EvidenceAdded", input.Title);
		return ProfessionalManagementProjection.ToEvidenceDto(evidence);
	}

	public async Task DeleteEvidenceAsync(Guid id)
	{
		var evidence = await evidenceRepository.GetAsync(id);
		(await assignmentRepository.GetAsync(evidence.ReviewAssignmentId)).EnsureEditable();
		await evidenceRepository.DeleteAsync(id, autoSave: true);
		await AuditAsync(evidence.ReviewAssignmentId, "EvidenceDeleted", evidence.Title);
	}

	public async Task<PerformanceFeedbackDto> AddFeedbackAsync(Guid id, PerformanceFeedbackRequest input)
	{
		(await assignmentRepository.GetAsync(id)).EnsureEditable();
		var feedback = new PerformanceFeedback(guidGenerator.Create(), CurrentTenant.Id, id, input.Source, input.ProviderName, input.FeedbackText);
		await feedbackRepository.InsertAsync(feedback, autoSave: true);
		await AuditAsync(id, "FeedbackAdded", $"Added {input.Source} feedback");
		return ProfessionalManagementProjection.ToFeedbackDto(feedback);
	}

	public async Task<List<ClassroomObservationDto>> GetObservationsAsync(Guid id) =>
		await reviewReader.GetObservationsAsync(id);

	public async Task<List<PerformanceFeedbackDto>> GetFeedbackAsync(Guid id) =>
		await reviewReader.GetFeedbackAsync(id);

	private async Task AuditAsync(Guid? reviewId, string eventType, string summary) =>
		await auditEventManager.RecordAsync(reviewId, eventType, summary);
}
