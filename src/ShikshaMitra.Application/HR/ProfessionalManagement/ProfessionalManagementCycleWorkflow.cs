using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementCycleWorkflow(
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	ProfessionalManagementAssignmentWorkflow assignmentWorkflow,
	PerformanceAuditEventManager auditEventManager,
	IGuidGenerator guidGenerator,
	IAsyncQueryableExecuter asyncExecuter) : ApplicationService, ITransientDependency
{
	private const int MaxPageSize = 100;

	public async Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(
		int page,
		int pageSize,
		string? search)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
		var query = await cycleRepository.GetQueryableAsync();
		query = query.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!));
		var totalCount = await asyncExecuter.CountAsync(query);
		var cycles = await asyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.PeriodStart)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));
		var ids = cycles.Select(x => x.Id).ToList();
		var assignments = await assignmentWorkflow.GetAssignmentsForCyclesAsync(ids);
		return new ProfessionalReviewCycleListResponse
		{
			TotalCount = totalCount,
			Items = cycles.Select(c => ProfessionalManagementProjection.ToCycleDto(c, assignments)).ToList()
		};
	}

	public async Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync(ProfessionalReviewCycleRequest input)
	{
		var cycle = new ProfessionalReviewCycle(
			guidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.Name,
			input.CycleType,
			input.PeriodStart,
			input.PeriodEnd,
			input.SelfReviewDueDate,
			input.ReviewerDueDate,
			input.FinalizationDueDate,
			input.RatingScaleMax,
			input.AllowNotApplicable,
			input.AudienceFilterJson,
			input.ReviewerWorkflowJson,
			ProfessionalReviewCycleStatus.Draft);
		await cycleRepository.InsertAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleCreated", $"Created performance cycle {cycle.Name}");
		return ProfessionalManagementProjection.ToCycleDto(cycle, []);
	}

	public async Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, ProfessionalReviewCycleRequest input)
	{
		var cycle = await cycleRepository.GetAsync(id);
		cycle.Update(
			input.AcademicYearId,
			input.Name,
			input.CycleType,
			input.PeriodStart,
			input.PeriodEnd,
			input.SelfReviewDueDate,
			input.ReviewerDueDate,
			input.FinalizationDueDate,
			input.RatingScaleMax,
			input.AllowNotApplicable,
			input.AudienceFilterJson,
			input.ReviewerWorkflowJson,
			cycle.Status);
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleUpdated", $"Updated performance cycle {cycle.Name}");
		return ProfessionalManagementProjection.ToCycleDto(cycle, await assignmentWorkflow.GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> PublishReviewCycleAsync(Guid id)
	{
		var cycle = await cycleRepository.GetAsync(id);
		cycle.Publish();
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CyclePublished", $"Published performance cycle {cycle.Name}");
		return ProfessionalManagementProjection.ToCycleDto(cycle, await assignmentWorkflow.GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> AssignEmployeesAsync(Guid id, AssignEmployeesRequest input)
	{
		var cycle = await cycleRepository.GetAsync(id);
		cycle.EnsureCanAssign();
		var createdCount = await assignmentWorkflow.AssignEmployeesAsync(cycle, input);

		if (createdCount > 0)
		{
			cycle.MarkInProgress();
			await cycleRepository.UpdateAsync(cycle, autoSave: true);
		}

		await AuditAsync(null, "CycleAssigned", $"Assigned {createdCount} staff to {cycle.Name}");
		return ProfessionalManagementProjection.ToCycleDto(cycle, await assignmentWorkflow.GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> MoveToCalibrationAsync(Guid id)
	{
		var cycle = await cycleRepository.GetAsync(id);
		cycle.MoveToCalibration();
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleCalibration", $"Moved {cycle.Name} to calibration");
		return ProfessionalManagementProjection.ToCycleDto(cycle, await assignmentWorkflow.GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> FinalizeCycleAsync(Guid id)
	{
		var cycle = await cycleRepository.GetAsync(id);
		cycle.FinalizeCycle();
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleFinalized", $"Finalized performance cycle {cycle.Name}");
		return ProfessionalManagementProjection.ToCycleDto(cycle, await assignmentWorkflow.GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> ArchiveCycleAsync(Guid id)
	{
		var cycle = await cycleRepository.GetAsync(id);
		cycle.Archive();
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleArchived", $"Archived performance cycle {cycle.Name}");
		return ProfessionalManagementProjection.ToCycleDto(cycle, await assignmentWorkflow.GetAssignmentsForCycleAsync(id));
	}

	private async Task AuditAsync(Guid? reviewId, string eventType, string summary) =>
		await auditEventManager.RecordAsync(reviewId, eventType, summary);
}
