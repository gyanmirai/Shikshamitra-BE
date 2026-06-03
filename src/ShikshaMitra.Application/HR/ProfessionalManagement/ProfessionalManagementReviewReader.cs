using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementReviewReader(
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<ClassroomObservation, Guid> observationRepository,
	IRepository<PerformanceEvidence, Guid> evidenceRepository,
	IRepository<PerformanceFeedback, Guid> feedbackRepository,
	IRepository<PerformanceAuditEvent, Guid> auditRepository,
	ProfessionalManagementReviewLookupReader lookupReader,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<ProfessionalReviewListResponse> GetReviewsAsync(
		Guid? cycleId,
		Guid? employeeId,
		ProfessionalReviewStatus? status,
		string? search)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		query = query
			.WhereIf(cycleId.HasValue, x => x.CycleId == cycleId)
			.WhereIf(employeeId.HasValue, x => x.EmployeeId == employeeId)
			.WhereIf(status.HasValue, x => x.Status == status);
		var assignments = await asyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime));
		var cycles = await lookupReader.GetCyclesByIdsAsync(assignments.Select(x => x.CycleId).Distinct().ToList());
		var employees = await lookupReader.GetEmployeesByIdsAsync(assignments.Select(x => x.EmployeeId).Distinct().ToList());
		var templates = await lookupReader.GetTemplatesByIdsAsync(assignments.Select(x => x.TemplateId).Distinct().ToList());
		var items = assignments
			.Select(x => ProfessionalManagementProjection.ToReviewDto(
				x,
				cycles.GetValueOrDefault(x.CycleId),
				employees.GetValueOrDefault(x.EmployeeId),
				templates.GetValueOrDefault(x.TemplateId)))
			.ToList();

		if (!string.IsNullOrWhiteSpace(search))
		{
			items = items
				.Where(x =>
					x.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					(x.EmployeeIdentifier?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
				.ToList();
		}

		return new ProfessionalReviewListResponse { TotalCount = items.Count, Items = items };
	}

	public async Task<ProfessionalReviewDto> GetReviewAsync(Guid id)
	{
		var assignment = await assignmentRepository.GetAsync(id);
		var cycle = await cycleRepository.GetAsync(assignment.CycleId);
		var employees = await lookupReader.GetEmployeesByIdsAsync([assignment.EmployeeId]);
		var templates = await lookupReader.GetTemplatesByIdsAsync([assignment.TemplateId]);
		var employee = employees.GetValueOrDefault(assignment.EmployeeId);
		var template = templates.GetValueOrDefault(assignment.TemplateId);
		var dto = ProfessionalManagementProjection.ToReviewDto(assignment, cycle, employee, template);
		dto.Template = template == null ? null : await MapTemplateAsync(template);
		dto.Stages = await lookupReader.MapStagesAsync(id);
		dto.Outcome = await lookupReader.MapOutcomeForReviewAsync(id);
		return dto;
	}

	public async Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId)
	{
		var reviews = await GetReviewsAsync(null, employeeId, null, null);
		var items = reviews.Items
			.OrderByDescending(x => x.FinalizedAt ?? x.SubmittedAt ?? DateTime.MinValue)
			.ToList();
		return new ProfessionalReviewListResponse { TotalCount = items.Count, Items = items };
	}

	public async Task<List<PerformanceTemplateDto>> MapTemplatesAsync(List<PerformanceTemplate> templates)
	{
		return await lookupReader.MapTemplatesAsync(templates);
	}

	public async Task<PerformanceTemplateDto> MapTemplateAsync(PerformanceTemplate template)
	{
		return await lookupReader.MapTemplateAsync(template);
	}

	public async Task<List<ClassroomObservationDto>> GetObservationsAsync(Guid id)
	{
		var query = await observationRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query
			.Where(x => x.ReviewAssignmentId == id)
			.OrderByDescending(x => x.ObservationDate));
		return rows.Select(ProfessionalManagementProjection.ToObservationDto).ToList();
	}

	public async Task<List<PerformanceFeedbackDto>> GetFeedbackAsync(Guid id)
	{
		var query = await feedbackRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query
			.Where(x => x.ReviewAssignmentId == id)
			.OrderByDescending(x => x.CreationTime));
		return rows.Select(ProfessionalManagementProjection.ToFeedbackDto).ToList();
	}

	public async Task<List<PerformanceEvidenceDto>> GetEvidenceAsync(Guid id)
	{
		var query = await evidenceRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query
			.Where(x => x.ReviewAssignmentId == id)
			.OrderBy(x => x.Title));
		return rows.Select(ProfessionalManagementProjection.ToEvidenceDto).ToList();
	}

	public async Task<List<PerformanceAuditEventDto>> GetAuditTrailAsync(Guid? reviewId)
	{
		var query = await auditRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query
			.WhereIf(reviewId.HasValue, x => x.ReviewAssignmentId == reviewId)
			.OrderByDescending(x => x.CreationTime)
			.Take(200));
		return rows.Select(ProfessionalManagementProjection.ToAuditEventDto).ToList();
	}

}
