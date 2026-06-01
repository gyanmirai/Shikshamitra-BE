using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.ProfessionalManagement;

[Authorize]
public class ProfessionalManagementAppService : ApplicationService, IProfessionalManagementAppService
{
	private const int MaxPageSize = 100;

	private readonly IRepository<ProfessionalReviewCycle, Guid> _cycleRepository;
	private readonly IRepository<ProfessionalReview, Guid> _legacyReviewRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<PerformanceTemplate, Guid> _templateRepository;
	private readonly IRepository<PerformanceTemplateSection, Guid> _sectionRepository;
	private readonly IRepository<PerformanceTemplateCriterion, Guid> _criterionRepository;
	private readonly IRepository<PerformanceReviewAssignment, Guid> _assignmentRepository;
	private readonly IRepository<PerformanceReviewStage, Guid> _stageRepository;
	private readonly IRepository<PerformanceReviewResponse, Guid> _responseRepository;
	private readonly IRepository<ClassroomObservation, Guid> _observationRepository;
	private readonly IRepository<PerformanceEvidence, Guid> _evidenceRepository;
	private readonly IRepository<PerformanceFeedback, Guid> _feedbackRepository;
	private readonly IRepository<PerformanceOutcome, Guid> _outcomeRepository;
	private readonly IRepository<DevelopmentPlan, Guid> _developmentPlanRepository;
	private readonly IRepository<PerformanceAuditEvent, Guid> _auditRepository;
	private readonly IGuidGenerator _guidGenerator;

	public ProfessionalManagementAppService(
		IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
		IRepository<ProfessionalReview, Guid> legacyReviewRepository,
		IRepository<Employee, Guid> employeeRepository,
		IRepository<PerformanceTemplate, Guid> templateRepository,
		IRepository<PerformanceTemplateSection, Guid> sectionRepository,
		IRepository<PerformanceTemplateCriterion, Guid> criterionRepository,
		IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
		IRepository<PerformanceReviewStage, Guid> stageRepository,
		IRepository<PerformanceReviewResponse, Guid> responseRepository,
		IRepository<ClassroomObservation, Guid> observationRepository,
		IRepository<PerformanceEvidence, Guid> evidenceRepository,
		IRepository<PerformanceFeedback, Guid> feedbackRepository,
		IRepository<PerformanceOutcome, Guid> outcomeRepository,
		IRepository<DevelopmentPlan, Guid> developmentPlanRepository,
		IRepository<PerformanceAuditEvent, Guid> auditRepository,
		IGuidGenerator guidGenerator)
	{
		_cycleRepository = cycleRepository;
		_legacyReviewRepository = legacyReviewRepository;
		_employeeRepository = employeeRepository;
		_templateRepository = templateRepository;
		_sectionRepository = sectionRepository;
		_criterionRepository = criterionRepository;
		_assignmentRepository = assignmentRepository;
		_stageRepository = stageRepository;
		_responseRepository = responseRepository;
		_observationRepository = observationRepository;
		_evidenceRepository = evidenceRepository;
		_feedbackRepository = feedbackRepository;
		_outcomeRepository = outcomeRepository;
		_developmentPlanRepository = developmentPlanRepository;
		_auditRepository = auditRepository;
		_guidGenerator = guidGenerator;
	}

	public async Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(int page = 1, int pageSize = 20, string? search = null)
	{
		await EnsureViewAsync();
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
		var query = await _cycleRepository.GetQueryableAsync();
		query = query.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!));
		var totalCount = await AsyncExecuter.CountAsync(query);
		var cycles = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.PeriodStart).Skip((page - 1) * pageSize).Take(pageSize));
		var ids = cycles.Select(x => x.Id).ToList();
		var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
		var assignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(x => ids.Contains(x.CycleId)));
		return new ProfessionalReviewCycleListResponse { TotalCount = totalCount, Items = cycles.Select(c => MapCycle(c, assignments)).ToList() };
	}

	public async Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync(ProfessionalReviewCycleRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageCycles, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = new ProfessionalReviewCycle(_guidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId, input.Name, input.CycleType, input.PeriodStart, input.PeriodEnd, input.SelfReviewDueDate, input.ReviewerDueDate, input.FinalizationDueDate, input.RatingScaleMax, input.AllowNotApplicable, input.AudienceFilterJson, input.ReviewerWorkflowJson, ProfessionalReviewCycleStatus.Draft);
		await _cycleRepository.InsertAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleCreated", $"Created performance cycle {cycle.Name}");
		return MapCycle(cycle, []);
	}

	public async Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, ProfessionalReviewCycleRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageCycles, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = await _cycleRepository.GetAsync(id);
		cycle.Update(input.AcademicYearId, input.Name, input.CycleType, input.PeriodStart, input.PeriodEnd, input.SelfReviewDueDate, input.ReviewerDueDate, input.FinalizationDueDate, input.RatingScaleMax, input.AllowNotApplicable, input.AudienceFilterJson, input.ReviewerWorkflowJson, cycle.Status);
		await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleUpdated", $"Updated performance cycle {cycle.Name}");
		return MapCycle(cycle, await GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> PublishReviewCycleAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageCycles, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = await _cycleRepository.GetAsync(id);
		cycle.Publish();
		await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CyclePublished", $"Published performance cycle {cycle.Name}");
		return MapCycle(cycle, await GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> AssignEmployeesAsync(Guid id, AssignEmployeesRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.AssignReviews, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = await _cycleRepository.GetAsync(id);
		cycle.EnsureCanAssign();
		var employees = await GetEligibleEmployeesAsync(input);
		var existing = await GetAssignmentsForCycleAsync(id);
		var createdCount = 0;
		foreach (var employee in employees.Where(employee => existing.All(x => x.EmployeeId != employee.Id)))
		{
			var templateId = input.TemplateId ?? await ResolveTemplateForEmployeeAsync(employee, input.StaffCategory);
			var assignment = new PerformanceReviewAssignment(_guidGenerator.Create(), CurrentTenant.Id, id, templateId, employee.Id, employee.ReportingManagerId, await FindReviewerByKeywordAsync(employee.Department, "hod", "coordinator", "head"), await FindReviewerByKeywordAsync(null, "principal", "vice principal", "management"));
			await _assignmentRepository.InsertAsync(assignment, autoSave: true);
			await EnsureStagesAsync(assignment);
			await EnsureLegacyReviewAsync(cycle.Id, employee.Id);
			existing.Add(assignment);
			createdCount++;
		}
		if (createdCount > 0)
		{
			cycle.MarkInProgress();
			await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		}
		await AuditAsync(null, "CycleAssigned", $"Assigned {createdCount} employees to {cycle.Name}");
		return MapCycle(cycle, await GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> MoveToCalibrationAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.Calibrate, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = await _cycleRepository.GetAsync(id);
		cycle.MoveToCalibration();
		await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleCalibration", $"Moved {cycle.Name} to calibration");
		return MapCycle(cycle, await GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> FinalizeCycleAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.Finalize, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = await _cycleRepository.GetAsync(id);
		cycle.FinalizeCycle();
		await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleFinalized", $"Finalized performance cycle {cycle.Name}");
		return MapCycle(cycle, await GetAssignmentsForCycleAsync(id));
	}

	public async Task<ProfessionalReviewCycleDto> ArchiveCycleAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageCycles, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var cycle = await _cycleRepository.GetAsync(id);
		cycle.Archive();
		await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		await AuditAsync(null, "CycleArchived", $"Archived performance cycle {cycle.Name}");
		return MapCycle(cycle, await GetAssignmentsForCycleAsync(id));
	}

	public async Task<PerformanceTemplateListResponse> GetTemplatesAsync(int page = 1, int pageSize = 20, string? search = null, PerformanceStaffCategory? staffCategory = null)
	{
		await EnsureViewAsync();
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
		var query = await _templateRepository.GetQueryableAsync();
		query = query.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!)).WhereIf(staffCategory.HasValue, x => x.StaffCategory == staffCategory);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var templates = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.StaffCategory).ThenBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize));
		return new PerformanceTemplateListResponse { TotalCount = totalCount, Items = await MapTemplatesAsync(templates) };
	}

	public async Task<PerformanceTemplateDto> CreateTemplateAsync(PerformanceTemplateRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageTemplates, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var template = await CreateTemplateInternalAsync(input);
		await AuditAsync(null, "TemplateCreated", $"Created performance template {template.Name}");
		return await MapTemplateAsync(template);
	}

	public async Task<PerformanceTemplateDto> UpdateTemplateAsync(Guid id, PerformanceTemplateRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageTemplates, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var template = await _templateRepository.GetAsync(id);
		template.Update(input.Name, input.StaffCategory, input.RoleName, input.RatingScaleMax, input.AllowNotApplicable, input.Description);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		await ReplaceTemplateChildrenAsync(template.Id, input);
		await AuditAsync(null, "TemplateUpdated", $"Updated performance template {template.Name}");
		return await MapTemplateAsync(template);
	}

	public async Task<PerformanceTemplateDto> PublishTemplateAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageTemplates, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var template = await _templateRepository.GetAsync(id);
		var sections = await GetSectionsAsync(template.Id);
		var criteria = await GetCriteriaAsync(template.Id);
		template.Publish(sections.Sum(x => x.Weight), HasValidRubric(sections, criteria));
		await _templateRepository.UpdateAsync(template, autoSave: true);
		await AuditAsync(null, "TemplatePublished", $"Published performance template {template.Name}");
		return await MapTemplateAsync(template);
	}

	public async Task<PerformanceTemplateDto> CloneTemplateAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageTemplates, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var source = await MapTemplateAsync(await _templateRepository.GetAsync(id));
		var request = new PerformanceTemplateRequest
		{
			Name = $"{source.Name} Copy",
			StaffCategory = source.StaffCategory,
			RoleName = source.RoleName,
			RatingScaleMax = source.RatingScaleMax,
			AllowNotApplicable = source.AllowNotApplicable,
			Description = source.Description,
			Sections = source.Sections.Select(s => new PerformanceTemplateSectionRequest
			{
				Name = s.Name,
				Weight = s.Weight,
				DisplayOrder = s.DisplayOrder,
				Criteria = s.Criteria.Select(c => new PerformanceTemplateCriterionRequest { Name = c.Name, Description = c.Description, Weight = c.Weight, EvidenceRequired = c.EvidenceRequired, DisplayOrder = c.DisplayOrder }).ToList()
			}).ToList()
		};
		return await CreateTemplateAsync(request);
	}

	public async Task<PerformanceTemplateDto> ArchiveTemplateAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageTemplates, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var template = await _templateRepository.GetAsync(id);
		template.Archive();
		await _templateRepository.UpdateAsync(template, autoSave: true);
		await AuditAsync(null, "TemplateArchived", $"Archived performance template {template.Name}");
		return await MapTemplateAsync(template);
	}

	public async Task<DefaultPerformanceTemplatesResponse> GetDefaultTemplatesAsync()
	{
		await EnsureViewAsync();
		return new DefaultPerformanceTemplatesResponse { Items = GetDefaultTemplateRequests() };
	}

	public async Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, string? search = null)
	{
		await EnsureViewAsync();
		return await GetReviewsInternalAsync(id, null, null, search);
	}

	public async Task<ProfessionalReviewListResponse> GetReviewsAsync(Guid? cycleId = null, Guid? employeeId = null, ProfessionalReviewStatus? status = null, string? search = null)
	{
		await EnsureViewAsync();
		return await GetReviewsInternalAsync(cycleId, employeeId, status, search);
	}

	public async Task<ProfessionalReviewDto> GetReviewAsync(Guid id)
	{
		await EnsureViewAsync();
		return await GetReviewInternalAsync(id);
	}

	public async Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, ProfessionalReviewSaveRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
		var legacy = await _legacyReviewRepository.FindAsync(id);
		if (legacy != null)
		{
			legacy.SaveDraft(input.CommunicationScore, input.ProblemSolvingScore, input.Comments, input.Feedback, CurrentUser.Id);
			await _legacyReviewRepository.UpdateAsync(legacy, autoSave: true);
			return await MapLegacyReviewWithCycleAsync(legacy);
		}
		return await SaveStageInternalAsync(id, new PerformanceReviewStageRequest { StageType = PerformanceReviewStageType.SelfAppraisal, SummaryComment = input.Comments }, false);
	}

	public async Task<ProfessionalReviewDto> SubmitAsync(Guid id, ProfessionalReviewSaveRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
		var legacy = await _legacyReviewRepository.FindAsync(id);
		if (legacy != null)
		{
			legacy.Submit(input.CommunicationScore, input.ProblemSolvingScore, input.Comments, input.Feedback, CurrentUser.Id, Clock.Now);
			await _legacyReviewRepository.UpdateAsync(legacy, autoSave: true);
			return await MapLegacyReviewWithCycleAsync(legacy);
		}
		return await SaveStageInternalAsync(id, new PerformanceReviewStageRequest { StageType = PerformanceReviewStageType.SelfAppraisal, SummaryComment = input.Comments }, true);
	}

	public async Task<ProfessionalReviewDto> SaveSelfAppraisalAsync(Guid id, PerformanceReviewStageRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
		input.StageType = PerformanceReviewStageType.SelfAppraisal;
		return await SaveStageInternalAsync(id, input, false);
	}

	public async Task<ProfessionalReviewDto> SaveReviewerStageAsync(Guid id, PerformanceReviewStageRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		if (input.StageType == PerformanceReviewStageType.SelfAppraisal)
		{
			input.StageType = PerformanceReviewStageType.ReportingManager;
		}
		return await SaveStageInternalAsync(id, input, false);
	}

	public async Task<ProfessionalReviewDto> SubmitStageAsync(Guid id, PerformanceReviewStageRequest input)
	{
		var self = input.StageType == PerformanceReviewStageType.SelfAppraisal;
		await EnsureAnyPermissionAsync(self ? ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview : ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees, self ? ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View : ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		return await SaveStageInternalAsync(id, input, true);
	}

	public async Task<ProfessionalReviewDto> ReopenReviewAsync(Guid id, ReopenReviewRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageOutcomes, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var assignment = await _assignmentRepository.GetAsync(id);
		assignment.Reopen();
		await _assignmentRepository.UpdateAsync(assignment, autoSave: true);
		var stageQuery = await _stageRepository.GetQueryableAsync();
		var submittedStages = await AsyncExecuter.ToListAsync(stageQuery.Where(x => x.ReviewAssignmentId == id && x.Status == PerformanceReviewStageStatus.Submitted));
		foreach (var stage in submittedStages)
		{
			stage.Reopen(input.Reason);
			await _stageRepository.UpdateAsync(stage, autoSave: true);
		}
		await AuditAsync(id, "ReviewReopened", input.Reason);
		return await GetReviewInternalAsync(id);
	}

	public async Task<PerformanceOutcomeDto> SaveFinalOutcomeAsync(Guid id, PerformanceOutcomeRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageOutcomes, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var assignment = await _assignmentRepository.GetAsync(id);
		assignment.EnsureEditable();
		var score = input.FinalScore ?? await CalculateScoreAsync(assignment);
		var existing = await FindOutcomeAsync(id);
		var outcome = existing ?? new PerformanceOutcome(_guidGenerator.Create(), CurrentTenant.Id, id, score, input.FinalRating, input.Strengths, input.ImprovementAreas, input.Recommendation, input.RecommendationNotes, input.IncrementRecommended, input.PromotionRecommended);
		if (existing == null)
		{
			await _outcomeRepository.InsertAsync(outcome, autoSave: true);
		}
		else
		{
			existing.Update(score, input.FinalRating, input.Strengths, input.ImprovementAreas, input.Recommendation, input.RecommendationNotes, input.IncrementRecommended, input.PromotionRecommended);
			await _outcomeRepository.UpdateAsync(existing, autoSave: true);
		}
		assignment.FinalizeReview(score, input.FinalRating, Clock.Now);
		await _assignmentRepository.UpdateAsync(assignment, autoSave: true);
		await AuditAsync(id, "OutcomeSaved", $"Saved final outcome {input.FinalRating}");
		return MapOutcome(outcome);
	}

	public async Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId)
	{
		await EnsureViewAsync();
		var modern = await GetReviewsInternalAsync(null, employeeId, null, null);
		var reviewQuery = await _legacyReviewRepository.GetQueryableAsync();
		var reviews = await AsyncExecuter.ToListAsync(reviewQuery.Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.SubmittedAt ?? x.LastModificationTime ?? x.CreationTime));
		var cycles = await GetCyclesByIdsAsync(reviews.Select(x => x.CycleId).Distinct().ToList());
		var employee = await _employeeRepository.FindAsync(employeeId);
		var modernCycleIds = modern.Items.Select(x => x.CycleId).ToHashSet();
		var legacyItems = reviews.Where(x => cycles.ContainsKey(x.CycleId) && !modernCycleIds.Contains(x.CycleId)).Select(x => MapLegacyReview(x, cycles[x.CycleId], employee));
		var items = modern.Items.Concat(legacyItems).OrderByDescending(x => x.FinalizedAt ?? x.SubmittedAt ?? DateTime.MinValue).ToList();
		return new ProfessionalReviewListResponse { TotalCount = items.Count, Items = items };
	}

	public async Task<ClassroomObservationDto> AddObservationAsync(Guid id, ClassroomObservationRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ClassroomObservation, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		(await _assignmentRepository.GetAsync(id)).EnsureEditable();
		var review = await GetReviewInternalAsync(id);
		if (review.Template?.StaffCategory is not (PerformanceStaffCategory.Teacher or PerformanceStaffCategory.ClassTeacher or PerformanceStaffCategory.HodCoordinator))
		{
			throw new BusinessException(HRErrorCodes.PerformanceObservationTeacherOnly);
		}
		var observation = new ClassroomObservation(_guidGenerator.Create(), CurrentTenant.Id, id, input.ObserverEmployeeId, input.ObservationDate, input.ClassName, input.SubjectName, input.LessonObjectiveScore, input.StudentEngagementScore, input.ClassroomManagementScore, input.AssessmentScore, input.Notes);
		await _observationRepository.InsertAsync(observation, autoSave: true);
		await AuditAsync(id, "ObservationAdded", "Added classroom observation");
		return MapObservation(observation);
	}

	public async Task<List<ClassroomObservationDto>> GetObservationsAsync(Guid id)
	{
		await EnsureViewAsync();
		return await GetObservationsInternalAsync(id);
	}

	public async Task<PerformanceEvidenceDto> AddEvidenceAsync(Guid id, PerformanceEvidenceRequest input)
	{
		await EnsureEvidenceUploadPermissionAsync();
		(await _assignmentRepository.GetAsync(id)).EnsureEditable();
		var evidence = new PerformanceEvidence(_guidGenerator.Create(), CurrentTenant.Id, id, input.CriterionId, input.Title, input.FileName, input.Url, input.Notes);
		await _evidenceRepository.InsertAsync(evidence, autoSave: true);
		await AuditAsync(id, "EvidenceAdded", input.Title);
		return MapEvidence(evidence);
	}

	public async Task DeleteEvidenceAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var evidence = await _evidenceRepository.GetAsync(id);
		(await _assignmentRepository.GetAsync(evidence.ReviewAssignmentId)).EnsureEditable();
		await _evidenceRepository.DeleteAsync(id, autoSave: true);
		await AuditAsync(evidence.ReviewAssignmentId, "EvidenceDeleted", evidence.Title);
	}

	public async Task<PerformanceFeedbackDto> AddFeedbackAsync(Guid id, PerformanceFeedbackRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		(await _assignmentRepository.GetAsync(id)).EnsureEditable();
		var feedback = new PerformanceFeedback(_guidGenerator.Create(), CurrentTenant.Id, id, input.Source, input.ProviderName, input.FeedbackText);
		await _feedbackRepository.InsertAsync(feedback, autoSave: true);
		await AuditAsync(id, "FeedbackAdded", $"Added {input.Source} feedback");
		return MapFeedback(feedback);
	}

	public async Task<List<PerformanceFeedbackDto>> GetFeedbackAsync(Guid id)
	{
		await EnsureViewAsync();
		return await GetFeedbackInternalAsync(id);
	}

	public async Task<DevelopmentPlanDto> AddDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageOutcomes, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		(await _assignmentRepository.GetAsync(id)).EnsureEditable();
		var plan = new DevelopmentPlan(_guidGenerator.Create(), CurrentTenant.Id, id, input.Goal, input.ActionPlan, input.OwnerEmployeeId, input.TargetDate);
		plan.Update(input.Goal, input.ActionPlan, input.OwnerEmployeeId, input.TargetDate, input.Status);
		await _developmentPlanRepository.InsertAsync(plan, autoSave: true);
		await AuditAsync(id, "DevelopmentPlanAdded", input.Goal);
		return MapDevelopmentPlan(plan);
	}

	public async Task<DevelopmentPlanDto> UpdateDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageOutcomes, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		var plan = await _developmentPlanRepository.GetAsync(id);
		(await _assignmentRepository.GetAsync(plan.ReviewAssignmentId)).EnsureEditable();
		plan.Update(input.Goal, input.ActionPlan, input.OwnerEmployeeId, input.TargetDate, input.Status);
		await _developmentPlanRepository.UpdateAsync(plan, autoSave: true);
		await AuditAsync(plan.ReviewAssignmentId, "DevelopmentPlanUpdated", input.Goal);
		return MapDevelopmentPlan(plan);
	}

	public async Task<PerformanceDashboardDto> GetDashboardAsync()
	{
		await EnsureViewAsync();
		var cycleQuery = await _cycleRepository.GetQueryableAsync();
		var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
		var stageQuery = await _stageRepository.GetQueryableAsync();
		var outcomeQuery = await _outcomeRepository.GetQueryableAsync();
		var cycles = await AsyncExecuter.ToListAsync(cycleQuery.Where(x => x.Status == ProfessionalReviewCycleStatus.Published || x.Status == ProfessionalReviewCycleStatus.InProgress || x.Status == ProfessionalReviewCycleStatus.Calibration));
		var activeCycleIds = cycles.Select(x => x.Id).ToHashSet();
		var assignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(x => activeCycleIds.Contains(x.CycleId) && x.Status != ProfessionalReviewStatus.Finalized));
		var activeAssignmentIds = assignments.Select(x => x.Id).ToHashSet();
		var stages = await AsyncExecuter.ToListAsync(stageQuery.Where(x => activeAssignmentIds.Contains(x.ReviewAssignmentId) && x.Status != PerformanceReviewStageStatus.Submitted));
		return new PerformanceDashboardDto
		{
			ActiveCycles = cycles.Count,
			PendingSelfReviews = stages.Count(x => x.StageType == PerformanceReviewStageType.SelfAppraisal),
			PendingReviewerReviews = stages.Count(x => x.StageType != PerformanceReviewStageType.SelfAppraisal),
			OverdueReviews = CountOverdueReviews(cycles, assignments, stages, Clock.Now.Date),
			FinalizedReviews = await AsyncExecuter.CountAsync(assignmentQuery.Where(x => x.Status == ProfessionalReviewStatus.Finalized)),
			TrainingRecommendations = await AsyncExecuter.CountAsync(outcomeQuery.Where(x => x.Recommendation == PerformanceRecommendationType.Training)),
			IncrementRecommendations = await AsyncExecuter.CountAsync(outcomeQuery.Where(x => x.IncrementRecommended || x.Recommendation == PerformanceRecommendationType.Increment))
		};
	}

	public async Task<List<RatingDistributionDto>> GetRatingDistributionAsync(Guid? cycleId = null)
	{
		await EnsureViewAsync();
		var query = await _assignmentRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.WhereIf(cycleId.HasValue, x => x.CycleId == cycleId).Where(x => x.FinalRating != null));
		return rows.GroupBy(x => x.FinalRating!).Select(x => new RatingDistributionDto { Rating = x.Key, Count = x.Count() }).OrderBy(x => x.Rating).ToList();
	}

	public async Task<List<PendingActionDto>> GetPendingActionsAsync(Guid? cycleId = null)
	{
		await EnsureViewAsync();
		var reviews = (await GetReviewsInternalAsync(cycleId, null, null, null)).Items.Where(x => x.Status != ProfessionalReviewStatus.Finalized).Take(100).ToList();
		var cycles = await GetCyclesByIdsAsync(reviews.Select(x => x.CycleId).Distinct().ToList());
		var stageQuery = await _stageRepository.GetQueryableAsync();
		var reviewIds = reviews.Select(x => x.Id).ToList();
		var stages = await AsyncExecuter.ToListAsync(stageQuery.Where(x => reviewIds.Contains(x.ReviewAssignmentId) && x.Status != PerformanceReviewStageStatus.Submitted));
		var stagesByReview = stages.GroupBy(x => x.ReviewAssignmentId).ToDictionary(x => x.Key, x => x.OrderBy(stage => stage.StageType).First());
		return reviews.Select(x =>
		{
			stagesByReview.TryGetValue(x.Id, out var pendingStage);
			var dueDate = pendingStage?.StageType switch
			{
				PerformanceReviewStageType.SelfAppraisal => cycles.GetValueOrDefault(x.CycleId)?.SelfReviewDueDate,
				PerformanceReviewStageType.HrVerification or PerformanceReviewStageType.PrincipalManagement => cycles.GetValueOrDefault(x.CycleId)?.FinalizationDueDate,
				_ => cycles.GetValueOrDefault(x.CycleId)?.ReviewerDueDate
			};
			return new PendingActionDto { ReviewId = x.Id, EmployeeName = x.EmployeeName, Action = pendingStage == null ? "Finalize appraisal outcome" : $"{pendingStage.StageType} pending", DueDate = dueDate };
		}).ToList();
	}

	public async Task<List<DevelopmentPlanDto>> GetTrainingNeedsAsync(Guid? cycleId = null)
	{
		await EnsureViewAsync();
		var query = await _developmentPlanRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.Status != DevelopmentPlanStatus.Completed).OrderBy(x => x.TargetDate));
		if (cycleId.HasValue)
		{
			var ids = (await GetAssignmentsForCycleAsync(cycleId.Value)).Select(x => x.Id).ToHashSet();
			rows = rows.Where(x => ids.Contains(x.ReviewAssignmentId)).ToList();
		}
		return rows.Select(MapDevelopmentPlan).ToList();
	}

	public async Task<List<PerformanceOutcomeDto>> GetIncrementRecommendationsAsync(Guid? cycleId = null)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ViewConfidentialOutcome, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
		var query = await _outcomeRepository.GetQueryableAsync();
		var outcomes = await AsyncExecuter.ToListAsync(query.Where(x => x.IncrementRecommended || x.PromotionRecommended || x.Recommendation == PerformanceRecommendationType.Increment || x.Recommendation == PerformanceRecommendationType.Promotion));
		if (cycleId.HasValue)
		{
			var ids = (await GetAssignmentsForCycleAsync(cycleId.Value)).Select(x => x.Id).ToHashSet();
			outcomes = outcomes.Where(x => ids.Contains(x.ReviewAssignmentId)).ToList();
		}
		return outcomes.Select(MapOutcome).ToList();
	}

	public async Task<List<PerformanceAuditEventDto>> GetAuditTrailAsync(Guid? reviewId = null)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ViewAudit, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
		return await GetAuditTrailInternalAsync(reviewId);
	}

	public async Task<byte[]> GetReviewPdfAsync(Guid id)
	{
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ExportReports, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
		var review = await GetReviewInternalAsync(id);
		var observations = await GetObservationsInternalAsync(id);
		var evidence = await GetEvidenceInternalAsync(id);
		var feedback = await GetFeedbackInternalAsync(id);
		var audit = await GetAuditTrailInternalAsync(id);
		var criterionNames = review.Template?.Sections
			.SelectMany(section => section.Criteria)
			.ToDictionary(criterion => criterion.Id, criterion => criterion.Name) ?? new Dictionary<Guid, string>();
		return Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(28);
				page.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken4));
				page.Header().Column(column =>
				{
					column.Item().Text("Performance Management Appraisal").FontSize(18).Bold().FontColor("#0F172A");
					column.Item().Text("Indian school HR appraisal record").FontSize(9).FontColor(Colors.Grey.Darken1);
					column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
				});
				page.Content().PaddingTop(12).Column(column =>
				{
					column.Spacing(12);
					column.Item().Element(c => ComposePdfKeyValueBlock(c, "Employee And Cycle", new[] { ("Employee", review.EmployeeName), ("Employee Code", review.EmployeeIdentifier ?? "-"), ("Role", review.EmployeeRole ?? "-"), ("Cycle", review.CycleName), ("Template", review.TemplateName ?? "-"), ("Final Rating", review.FinalRating ?? "-"), ("Final Score", review.FinalScore?.ToString("0.##") ?? "-") }));
					column.Item().Text("Reviewer Stages").Bold().FontSize(12).FontColor("#0F172A");
					foreach (var stage in review.Stages.OrderBy(x => x.StageType))
					{
						column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(stageColumn =>
						{
							stageColumn.Item().Text($"{stage.StageType} - {stage.Status}").Bold();
							if (!string.IsNullOrWhiteSpace(stage.SummaryComment)) stageColumn.Item().Text(stage.SummaryComment);
							foreach (var response in stage.Responses.Take(12))
							{
								var criterionName = criterionNames.GetValueOrDefault(response.CriterionId) ?? $"Criterion {response.CriterionId}";
								stageColumn.Item().Text($"{criterionName}: {(response.IsNotApplicable ? "Not Applicable" : response.Rating?.ToString() ?? "-")} - {response.Comment ?? "-"}").FontSize(8);
							}
						});
					}
					column.Item().Element(c => ComposePdfSimpleList(c, "Classroom Observations", observations.Select(x => $"{x.ObservationDate:dd MMM yyyy}: {x.ClassName ?? "-"} {x.SubjectName ?? "-"} - {x.Notes ?? "-"}")));
					column.Item().Element(c => ComposePdfSimpleList(c, "Evidence Summary", evidence.Select(x => $"{x.Title}: {x.FileName ?? x.Url ?? x.Notes ?? "-"}")));
					column.Item().Element(c => ComposePdfSimpleList(c, "Qualitative Feedback", feedback.Select(x => $"{x.Source}: {x.ProviderName ?? "Anonymous"} - {x.FeedbackText}")));
					if (review.Outcome != null)
					{
						column.Item().Element(c => ComposePdfKeyValueBlock(c, "Outcome And Recommendations", new[] { ("Strengths", review.Outcome.Strengths ?? "-"), ("Improvement Areas", review.Outcome.ImprovementAreas ?? "-"), ("Recommendation", review.Outcome.Recommendation.ToString()), ("Increment Recommended", review.Outcome.IncrementRecommended ? "Yes" : "No"), ("Promotion Recommended", review.Outcome.PromotionRecommended ? "Yes" : "No"), ("Notes", review.Outcome.RecommendationNotes ?? "-") }));
					}
					column.Item().Element(c => ComposePdfSimpleList(c, "Audit Trail", audit.Take(10).Select(x => $"{x.CreationTime:dd MMM yyyy HH:mm}: {x.EventType} - {x.Summary}")));
					column.Item().PaddingTop(12).Text("Acknowledgement: appraisal outcomes are recommendations only and do not update payroll automatically.").Italic().FontColor(Colors.Grey.Darken1);
				});
				page.Footer().AlignCenter().Text(text =>
				{
					text.Span("Generated ");
					text.Span(Clock.Now.ToString("dd MMM yyyy"));
					text.Span(" | Page ");
					text.CurrentPageNumber();
					text.Span(" of ");
					text.TotalPages();
				});
			});
		}).GeneratePdf();
	}

	private async Task<ProfessionalReviewListResponse> GetReviewsInternalAsync(Guid? cycleId, Guid? employeeId, ProfessionalReviewStatus? status, string? search)
	{
		var query = await _assignmentRepository.GetQueryableAsync();
		query = query.WhereIf(cycleId.HasValue, x => x.CycleId == cycleId).WhereIf(employeeId.HasValue, x => x.EmployeeId == employeeId).WhereIf(status.HasValue, x => x.Status == status);
		var assignments = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime));
		var cycles = await GetCyclesByIdsAsync(assignments.Select(x => x.CycleId).Distinct().ToList());
		var employees = await GetEmployeesByIdsAsync(assignments.Select(x => x.EmployeeId).Distinct().ToList());
		var templates = await GetTemplatesByIdsAsync(assignments.Select(x => x.TemplateId).Distinct().ToList());
		var items = assignments.Select(x => MapReview(x, cycles.GetValueOrDefault(x.CycleId), employees.GetValueOrDefault(x.EmployeeId), templates.GetValueOrDefault(x.TemplateId))).ToList();
		if (!string.IsNullOrWhiteSpace(search))
		{
			items = items.Where(x => x.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) || (x.EmployeeIdentifier?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
		}
		return new ProfessionalReviewListResponse { TotalCount = items.Count, Items = items };
	}

	private async Task<ProfessionalReviewDto> GetReviewInternalAsync(Guid id)
	{
		var assignment = await _assignmentRepository.GetAsync(id);
		var cycle = await _cycleRepository.GetAsync(assignment.CycleId);
		var employee = await _employeeRepository.FindAsync(assignment.EmployeeId);
		var template = await _templateRepository.FindAsync(assignment.TemplateId);
		var dto = MapReview(assignment, cycle, employee, template);
		dto.Template = template == null ? null : await MapTemplateAsync(template);
		dto.Stages = await MapStagesAsync(id);
		dto.Outcome = await MapOutcomeForReviewAsync(id);
		return dto;
	}

	private async Task<ProfessionalReviewDto> SaveStageInternalAsync(Guid id, PerformanceReviewStageRequest input, bool submit)
	{
		var assignment = await _assignmentRepository.GetAsync(id);
		assignment.EnsureEditable();
		var stage = await FindStageAsync(id, input.StageType);
		var isNewStage = stage == null;
		stage ??= new PerformanceReviewStage(_guidGenerator.Create(), CurrentTenant.Id, id, input.StageType, ResolveStageReviewer(assignment, input.StageType));
		if (isNewStage) await _stageRepository.InsertAsync(stage, autoSave: true);
		if (submit)
		{
			await ValidateStageSubmissionAsync(assignment, input);
			stage.Submit(input.SummaryComment, Clock.Now);
		}
		else
		{
			stage.SaveDraft(input.SummaryComment);
		}
		await _stageRepository.UpdateAsync(stage, autoSave: true);
		await UpsertResponsesAsync(assignment, stage, input.Responses);
		assignment.MarkInProgress();
		await _assignmentRepository.UpdateAsync(assignment, autoSave: true);
		await AuditAsync(id, submit ? "StageSubmitted" : "StageSaved", $"{input.StageType} {(submit ? "submitted" : "saved")}");
		return await GetReviewInternalAsync(id);
	}

	private async Task EnsureViewAsync() =>
		await EnsureAnyPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.View, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);

	private async Task EnsureEvidenceUploadPermissionAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview) ||
		    await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees) ||
		    await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View) ||
		    await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add))
		{
			return;
		}

		throw new AbpAuthorizationException($"Required permission: {ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview}");
	}

	private async Task EnsureAnyPermissionAsync(string primaryPermission, string legacyPermission)
	{
		if (await AuthorizationService.IsGrantedAsync(primaryPermission) || await AuthorizationService.IsGrantedAsync(legacyPermission))
		{
			return;
		}
		throw new AbpAuthorizationException($"Required permission: {primaryPermission}");
	}

	private static int CountOverdueReviews(IReadOnlyCollection<ProfessionalReviewCycle> cycles, IReadOnlyCollection<PerformanceReviewAssignment> assignments, IReadOnlyCollection<PerformanceReviewStage> stages, DateTime today)
	{
		var cycleById = cycles.ToDictionary(x => x.Id);
		var stageByReview = stages.GroupBy(x => x.ReviewAssignmentId).ToDictionary(x => x.Key, x => x.ToList());
		var count = 0;
		foreach (var assignment in assignments)
		{
			if (!cycleById.TryGetValue(assignment.CycleId, out var cycle) || !stageByReview.TryGetValue(assignment.Id, out var reviewStages)) continue;
			count += reviewStages.Count(stage =>
			{
				var dueDate = stage.StageType switch
				{
					PerformanceReviewStageType.SelfAppraisal => cycle.SelfReviewDueDate,
					PerformanceReviewStageType.HrVerification or PerformanceReviewStageType.PrincipalManagement => cycle.FinalizationDueDate,
					_ => cycle.ReviewerDueDate
				};
				return dueDate.HasValue && dueDate.Value.Date < today;
			});
		}
		return count;
	}

	private async Task ValidateStageSubmissionAsync(PerformanceReviewAssignment assignment, PerformanceReviewStageRequest input)
	{
		var template = await _templateRepository.GetAsync(assignment.TemplateId);
		var criteria = await GetCriteriaAsync(assignment.TemplateId);
		var criterionIds = criteria.Select(x => x.Id).ToHashSet();
		var responseCriterionIds = input.Responses.Select(x => x.CriterionId).ToList();
		if (responseCriterionIds.Count != responseCriterionIds.Distinct().Count() ||
		    responseCriterionIds.Any(x => !criterionIds.Contains(x)))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCriterionInvalid);
		}

		foreach (var response in input.Responses.Where(x => !x.IsNotApplicable))
		{
			if (!response.Rating.HasValue || response.Rating.Value < 1 || response.Rating.Value > template.RatingScaleMax)
			{
				throw new BusinessException(HRErrorCodes.PerformanceReviewCriterionInvalid).WithData("CriterionId", response.CriterionId).WithData("RatingScaleMax", template.RatingScaleMax);
			}
		}
		if (!template.AllowNotApplicable && input.Responses.Any(x => x.IsNotApplicable))
		{
			throw new BusinessException(HRErrorCodes.PerformanceReviewCriterionInvalid);
		}

		var requiredCriterionIds = criteria.Where(x => x.EvidenceRequired).Select(x => x.Id).ToHashSet();
		if (requiredCriterionIds.Count == 0) return;
		var evidenceQuery = await _evidenceRepository.GetQueryableAsync();
		var evidenceCriterionIds = (await AsyncExecuter.ToListAsync(evidenceQuery.Where(x => x.ReviewAssignmentId == assignment.Id && x.CriterionId.HasValue))).Select(x => x.CriterionId!.Value).ToHashSet();
		var submittedCriterionIds = input.Responses.Where(x => !x.IsNotApplicable).Select(x => x.CriterionId).ToHashSet();
		if (requiredCriterionIds.Any(x => submittedCriterionIds.Contains(x) && !evidenceCriterionIds.Contains(x)))
		{
			throw new BusinessException(HRErrorCodes.PerformanceEvidenceRequired);
		}
	}

	private async Task UpsertResponsesAsync(PerformanceReviewAssignment assignment, PerformanceReviewStage stage, List<PerformanceReviewResponseRequest> responses)
	{
		var query = await _responseRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(query.Where(x => x.StageId == stage.Id));
		foreach (var input in responses)
		{
			var row = existing.FirstOrDefault(x => x.CriterionId == input.CriterionId);
			if (row == null)
			{
				row = new PerformanceReviewResponse(_guidGenerator.Create(), CurrentTenant.Id, assignment.Id, stage.Id, input.CriterionId, input.Rating, input.IsNotApplicable, input.Comment);
				await _responseRepository.InsertAsync(row, autoSave: true);
			}
			else
			{
				row.Update(input.Rating, input.IsNotApplicable, input.Comment);
				await _responseRepository.UpdateAsync(row, autoSave: true);
			}
		}
	}

	private async Task<decimal> CalculateScoreAsync(PerformanceReviewAssignment assignment)
	{
		var sections = await GetSectionsAsync(assignment.TemplateId);
		var criteria = await GetCriteriaAsync(assignment.TemplateId);
		var responseQuery = await _responseRepository.GetQueryableAsync();
		var responses = await AsyncExecuter.ToListAsync(responseQuery.Where(x => x.ReviewAssignmentId == assignment.Id));
		var template = await _templateRepository.GetAsync(assignment.TemplateId);
		return PerformanceScoreCalculator.CalculateFinalScore(sections, criteria, responses, template.RatingScaleMax);
	}

	private async Task<PerformanceTemplate> CreateTemplateInternalAsync(PerformanceTemplateRequest input)
	{
		var template = new PerformanceTemplate(_guidGenerator.Create(), CurrentTenant.Id, input.Name, input.StaffCategory, input.RoleName, input.Description);
		template.Update(input.Name, input.StaffCategory, input.RoleName, input.RatingScaleMax, input.AllowNotApplicable, input.Description);
		await _templateRepository.InsertAsync(template, autoSave: true);
		await ReplaceTemplateChildrenAsync(template.Id, input);
		return template;
	}

	private async Task ReplaceTemplateChildrenAsync(Guid templateId, PerformanceTemplateRequest input)
	{
		foreach (var existingCriterion in await GetCriteriaAsync(templateId)) await _criterionRepository.DeleteAsync(existingCriterion, autoSave: true);
		foreach (var existingSection in await GetSectionsAsync(templateId)) await _sectionRepository.DeleteAsync(existingSection, autoSave: true);
		foreach (var sectionInput in input.Sections)
		{
			var section = new PerformanceTemplateSection(_guidGenerator.Create(), CurrentTenant.Id, templateId, sectionInput.Name, sectionInput.Weight, sectionInput.DisplayOrder);
			await _sectionRepository.InsertAsync(section, autoSave: true);
			foreach (var criterionInput in sectionInput.Criteria)
			{
				var criterion = new PerformanceTemplateCriterion(_guidGenerator.Create(), CurrentTenant.Id, templateId, section.Id, criterionInput.Name, criterionInput.Description, criterionInput.Weight, criterionInput.EvidenceRequired, criterionInput.DisplayOrder);
				await _criterionRepository.InsertAsync(criterion, autoSave: true);
			}
		}
	}

	private async Task EnsureLegacyReviewAsync(Guid cycleId, Guid employeeId)
	{
		var query = await _legacyReviewRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.CycleId == cycleId && x.EmployeeId == employeeId))) return;
		await _legacyReviewRepository.InsertAsync(new ProfessionalReview(_guidGenerator.Create(), CurrentTenant.Id, cycleId, employeeId, CurrentUser.Id), autoSave: true);
	}

	private async Task EnsureStagesAsync(PerformanceReviewAssignment assignment)
	{
		foreach (var stageType in Enum.GetValues<PerformanceReviewStageType>())
		{
			await _stageRepository.InsertAsync(new PerformanceReviewStage(_guidGenerator.Create(), CurrentTenant.Id, assignment.Id, stageType, ResolveStageReviewer(assignment, stageType)), autoSave: true);
		}
	}

	private static Guid? ResolveStageReviewer(PerformanceReviewAssignment assignment, PerformanceReviewStageType stageType) => stageType switch
	{
		PerformanceReviewStageType.ReportingManager => assignment.ReportingManagerEmployeeId,
		PerformanceReviewStageType.HodCoordinator => assignment.HodEmployeeId,
		PerformanceReviewStageType.PrincipalManagement => assignment.PrincipalEmployeeId,
		_ => null
	};

	private async Task<List<Employee>> GetEligibleEmployeesAsync(AssignEmployeesRequest input)
	{
		var query = await _employeeRepository.GetQueryableAsync();
		query = query.WhereIf(input.EmployeeIds.Count > 0, x => input.EmployeeIds.Contains(x.Id)).WhereIf(input.ActiveEmployeesOnly, x => x.EmployeeStatusType == null || x.EmployeeStatusType == EmployeeStatusType.Active).WhereIf(!string.IsNullOrWhiteSpace(input.Department), x => x.Department != null && x.Department.Contains(input.Department!)).WhereIf(!string.IsNullOrWhiteSpace(input.Designation), x => x.Designation != null && x.Designation.Contains(input.Designation!));
		var employees = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName));
		return input.StaffCategory.HasValue ? employees.Where(x => InferStaffCategory(x) == input.StaffCategory.Value).ToList() : employees;
	}

	private async Task<Guid> ResolveTemplateForEmployeeAsync(Employee employee, PerformanceStaffCategory? requestedCategory)
	{
		var staffCategory = requestedCategory ?? InferStaffCategory(employee);
		var query = await _templateRepository.GetQueryableAsync();
		var template = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Status == PerformanceTemplateStatus.Published && x.StaffCategory == staffCategory).OrderBy(x => x.CreationTime));
		template ??= await AsyncExecuter.FirstOrDefaultAsync(query.OrderBy(x => x.CreationTime));
		if (template != null) return template.Id;
		var request = GetDefaultTemplateRequests().FirstOrDefault(x => x.StaffCategory == staffCategory) ?? GetDefaultTemplateRequests().First(x => x.StaffCategory == PerformanceStaffCategory.Admin);
		template = await CreateTemplateInternalAsync(request);
		var sections = await GetSectionsAsync(template.Id);
		var criteria = await GetCriteriaAsync(template.Id);
		template.Publish(sections.Sum(x => x.Weight), HasValidRubric(sections, criteria));
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return template.Id;
	}

	private static bool HasValidRubric(IReadOnlyCollection<PerformanceTemplateSection> sections, IReadOnlyCollection<PerformanceTemplateCriterion> criteria)
	{
		if (sections.Count == 0 || criteria.Count == 0)
		{
			return false;
		}

		return sections.All(section =>
		{
			var sectionCriteria = criteria.Where(criterion => criterion.SectionId == section.Id).ToList();
			return sectionCriteria.Count > 0 && sectionCriteria.Sum(criterion => criterion.Weight) > 0;
		});
	}

	private async Task<Guid?> FindReviewerByKeywordAsync(string? preferredDepartment, params string[] keywords)
	{
		var query = await _employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(query.Where(x => x.EmployeeStatusType == null || x.EmployeeStatusType == EmployeeStatusType.Active));
		return employees.Where(x => string.IsNullOrWhiteSpace(preferredDepartment) || string.Equals(x.Department, preferredDepartment, StringComparison.OrdinalIgnoreCase)).Concat(employees).FirstOrDefault(x => keywords.Any(keyword => (x.Designation?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) || (x.Role != null && x.Role.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))))?.Id;
	}

	private static PerformanceStaffCategory InferStaffCategory(Employee employee)
	{
		var text = $"{employee.Designation} {employee.Department} {employee.Role?.Name}".ToLowerInvariant();
		if (text.Contains("class teacher")) return PerformanceStaffCategory.ClassTeacher;
		if (text.Contains("hod") || text.Contains("coordinator") || text.Contains("head of department")) return PerformanceStaffCategory.HodCoordinator;
		if (text.Contains("principal") || text.Contains("vice principal")) return PerformanceStaffCategory.PrincipalVicePrincipal;
		if (text.Contains("account")) return PerformanceStaffCategory.Accounts;
		if (text.Contains("librarian") || text.Contains("lab")) return PerformanceStaffCategory.LibrarianLabAssistant;
		if (text.Contains("transport") || text.Contains("driver") || text.Contains("conductor")) return PerformanceStaffCategory.Transport;
		if (text.Contains("security") || text.Contains("housekeeping")) return PerformanceStaffCategory.SecurityHousekeeping;
		if (text.Contains("it") || text.Contains("support")) return PerformanceStaffCategory.ItSupport;
		if (text.Contains("teacher") || text.Contains("faculty") || text.Contains("academic")) return PerformanceStaffCategory.Teacher;
		return PerformanceStaffCategory.Admin;
	}

	private async Task<List<PerformanceReviewAssignment>> GetAssignmentsForCycleAsync(Guid cycleId)
	{
		var query = await _assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => x.CycleId == cycleId));
	}

	private async Task<PerformanceReviewStage?> FindStageAsync(Guid reviewId, PerformanceReviewStageType stageType)
	{
		var query = await _stageRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.ReviewAssignmentId == reviewId && x.StageType == stageType));
	}

	private async Task<PerformanceOutcome?> FindOutcomeAsync(Guid reviewId)
	{
		var query = await _outcomeRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.ReviewAssignmentId == reviewId));
	}

	private async Task<List<PerformanceTemplateSection>> GetSectionsAsync(Guid templateId)
	{
		var query = await _sectionRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => x.TemplateId == templateId).OrderBy(x => x.DisplayOrder));
	}

	private async Task<List<PerformanceTemplateCriterion>> GetCriteriaAsync(Guid templateId)
	{
		var query = await _criterionRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => x.TemplateId == templateId).OrderBy(x => x.DisplayOrder));
	}

	private async Task<Dictionary<Guid, ProfessionalReviewCycle>> GetCyclesByIdsAsync(List<Guid> ids)
	{
		var query = await _cycleRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, Employee>> GetEmployeesByIdsAsync(List<Guid> ids)
	{
		var query = await _employeeRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, PerformanceTemplate>> GetTemplatesByIdsAsync(List<Guid> ids)
	{
		var query = await _templateRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	private async Task<List<PerformanceTemplateDto>> MapTemplatesAsync(List<PerformanceTemplate> templates)
	{
		var result = new List<PerformanceTemplateDto>();
		foreach (var template in templates) result.Add(await MapTemplateAsync(template));
		return result;
	}

	private async Task<PerformanceTemplateDto> MapTemplateAsync(PerformanceTemplate template)
	{
		var sections = await GetSectionsAsync(template.Id);
		var criteria = await GetCriteriaAsync(template.Id);
		return new PerformanceTemplateDto { Id = template.Id, Name = template.Name, StaffCategory = template.StaffCategory, RoleName = template.RoleName, RatingScaleMax = template.RatingScaleMax, AllowNotApplicable = template.AllowNotApplicable, Status = template.Status, Description = template.Description, Sections = sections.Select(s => new PerformanceTemplateSectionDto { Id = s.Id, TemplateId = s.TemplateId, Name = s.Name, Weight = s.Weight, DisplayOrder = s.DisplayOrder, Criteria = criteria.Where(c => c.SectionId == s.Id).Select(MapCriterion).ToList() }).ToList() };
	}

	private static PerformanceTemplateCriterionDto MapCriterion(PerformanceTemplateCriterion criterion) => new() { Id = criterion.Id, TemplateId = criterion.TemplateId, SectionId = criterion.SectionId, Name = criterion.Name, Description = criterion.Description, Weight = criterion.Weight, EvidenceRequired = criterion.EvidenceRequired, DisplayOrder = criterion.DisplayOrder };

	private static ProfessionalReviewCycleDto MapCycle(ProfessionalReviewCycle cycle, IReadOnlyCollection<PerformanceReviewAssignment> assignments) => new() { Id = cycle.Id, AcademicYearId = cycle.AcademicYearId, Name = cycle.Name, CycleType = cycle.CycleType, PeriodStart = cycle.PeriodStart, PeriodEnd = cycle.PeriodEnd, SelfReviewDueDate = cycle.SelfReviewDueDate, ReviewerDueDate = cycle.ReviewerDueDate, FinalizationDueDate = cycle.FinalizationDueDate, RatingScaleMax = cycle.RatingScaleMax, AllowNotApplicable = cycle.AllowNotApplicable, AudienceFilterJson = cycle.AudienceFilterJson, ReviewerWorkflowJson = cycle.ReviewerWorkflowJson, Status = cycle.Status, AssignedCount = assignments.Count(x => x.CycleId == cycle.Id), FinalizedCount = assignments.Count(x => x.CycleId == cycle.Id && x.Status == ProfessionalReviewStatus.Finalized) };

	private static ProfessionalReviewDto MapReview(PerformanceReviewAssignment assignment, ProfessionalReviewCycle? cycle, Employee? employee, PerformanceTemplate? template) => new() { Id = assignment.Id, CycleId = assignment.CycleId, CycleName = cycle?.Name ?? "Unknown cycle", EmployeeId = assignment.EmployeeId, EmployeeName = employee?.EmployeeName ?? "Unknown employee", EmployeeIdentifier = employee?.EmployeeCode, EmployeeRole = employee?.Role?.Name, TemplateId = assignment.TemplateId, TemplateName = template?.Name, FinalScore = assignment.FinalScore, FinalRating = assignment.FinalRating, Status = assignment.Status, FinalizedAt = assignment.FinalizedAt };

	private async Task<List<PerformanceReviewStageDto>> MapStagesAsync(Guid reviewId)
	{
		var stageQuery = await _stageRepository.GetQueryableAsync();
		var stages = await AsyncExecuter.ToListAsync(stageQuery.Where(x => x.ReviewAssignmentId == reviewId).OrderBy(x => x.StageType));
		var responseQuery = await _responseRepository.GetQueryableAsync();
		var responses = await AsyncExecuter.ToListAsync(responseQuery.Where(x => x.ReviewAssignmentId == reviewId));
		return stages.Select(s => new PerformanceReviewStageDto { Id = s.Id, ReviewAssignmentId = s.ReviewAssignmentId, StageType = s.StageType, ReviewerEmployeeId = s.ReviewerEmployeeId, Status = s.Status, SummaryComment = s.SummaryComment, SubmittedAt = s.SubmittedAt, Responses = responses.Where(r => r.StageId == s.Id).Select(MapResponse).ToList() }).ToList();
	}

	private static PerformanceReviewResponseDto MapResponse(PerformanceReviewResponse response) => new() { Id = response.Id, CriterionId = response.CriterionId, Rating = response.Rating, IsNotApplicable = response.IsNotApplicable, Comment = response.Comment };

	private async Task<PerformanceOutcomeDto?> MapOutcomeForReviewAsync(Guid reviewId)
	{
		var outcome = await FindOutcomeAsync(reviewId);
		return outcome == null ? null : MapOutcome(outcome);
	}

	private static PerformanceOutcomeDto MapOutcome(PerformanceOutcome outcome) => new() { Id = outcome.Id, ReviewAssignmentId = outcome.ReviewAssignmentId, FinalScore = outcome.FinalScore, FinalRating = outcome.FinalRating, Strengths = outcome.Strengths, ImprovementAreas = outcome.ImprovementAreas, Recommendation = outcome.Recommendation, RecommendationNotes = outcome.RecommendationNotes, IncrementRecommended = outcome.IncrementRecommended, PromotionRecommended = outcome.PromotionRecommended };
	private static ClassroomObservationDto MapObservation(ClassroomObservation observation) => new() { Id = observation.Id, ReviewAssignmentId = observation.ReviewAssignmentId, ObserverEmployeeId = observation.ObserverEmployeeId, ObservationDate = observation.ObservationDate, ClassName = observation.ClassName, SubjectName = observation.SubjectName, LessonObjectiveScore = observation.LessonObjectiveScore, StudentEngagementScore = observation.StudentEngagementScore, ClassroomManagementScore = observation.ClassroomManagementScore, AssessmentScore = observation.AssessmentScore, Notes = observation.Notes };
	private static PerformanceEvidenceDto MapEvidence(PerformanceEvidence evidence) => new() { Id = evidence.Id, ReviewAssignmentId = evidence.ReviewAssignmentId, CriterionId = evidence.CriterionId, Title = evidence.Title, FileName = evidence.FileName, Url = evidence.Url, Notes = evidence.Notes };
	private static PerformanceFeedbackDto MapFeedback(PerformanceFeedback feedback) => new() { Id = feedback.Id, ReviewAssignmentId = feedback.ReviewAssignmentId, Source = feedback.Source, ProviderName = feedback.ProviderName, FeedbackText = feedback.FeedbackText };
	private static DevelopmentPlanDto MapDevelopmentPlan(DevelopmentPlan plan) => new() { Id = plan.Id, ReviewAssignmentId = plan.ReviewAssignmentId, Goal = plan.Goal, ActionPlan = plan.ActionPlan, OwnerEmployeeId = plan.OwnerEmployeeId, TargetDate = plan.TargetDate, Status = plan.Status };
	private static PerformanceAuditEventDto MapAuditEvent(PerformanceAuditEvent auditEvent) => new() { Id = auditEvent.Id, ReviewAssignmentId = auditEvent.ReviewAssignmentId, EventType = auditEvent.EventType, Summary = auditEvent.Summary, MetadataJson = auditEvent.MetadataJson, CreationTime = auditEvent.CreationTime };

	private async Task<List<ClassroomObservationDto>> GetObservationsInternalAsync(Guid id)
	{
		var query = await _observationRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.ReviewAssignmentId == id).OrderByDescending(x => x.ObservationDate));
		return rows.Select(MapObservation).ToList();
	}

	private async Task<List<PerformanceFeedbackDto>> GetFeedbackInternalAsync(Guid id)
	{
		var query = await _feedbackRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.ReviewAssignmentId == id).OrderByDescending(x => x.CreationTime));
		return rows.Select(MapFeedback).ToList();
	}

	private async Task<List<PerformanceEvidenceDto>> GetEvidenceInternalAsync(Guid id)
	{
		var query = await _evidenceRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.ReviewAssignmentId == id).OrderBy(x => x.Title));
		return rows.Select(MapEvidence).ToList();
	}

	private async Task<List<PerformanceAuditEventDto>> GetAuditTrailInternalAsync(Guid? reviewId)
	{
		var query = await _auditRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.WhereIf(reviewId.HasValue, x => x.ReviewAssignmentId == reviewId).OrderByDescending(x => x.CreationTime).Take(200));
		return rows.Select(MapAuditEvent).ToList();
	}

	private async Task<ProfessionalReviewDto> MapLegacyReviewWithCycleAsync(ProfessionalReview review)
	{
		var cycle = await _cycleRepository.GetAsync(review.CycleId);
		var employee = await _employeeRepository.FindAsync(review.EmployeeId);
		return MapLegacyReview(review, cycle, employee);
	}

	private static ProfessionalReviewDto MapLegacyReview(ProfessionalReview review, ProfessionalReviewCycle cycle, Employee? employee) => new() { Id = review.Id, CycleId = review.CycleId, CycleName = cycle.Name, EmployeeId = review.EmployeeId, EmployeeName = employee?.EmployeeName ?? "Unknown employee", EmployeeIdentifier = employee?.EmployeeCode, EmployeeRole = employee?.Role?.Name, ReviewerUserId = review.ReviewerUserId, CommunicationScore = review.CommunicationScore, ProblemSolvingScore = review.ProblemSolvingScore, Comments = review.Comments, Feedback = review.Feedback, Status = review.Status, SubmittedAt = review.SubmittedAt };

	private async Task AuditAsync(Guid? reviewId, string eventType, string summary) =>
		await _auditRepository.InsertAsync(new PerformanceAuditEvent(_guidGenerator.Create(), CurrentTenant.Id, reviewId, eventType, summary), autoSave: true);

	private static void ComposePdfKeyValueBlock(IContainer container, string title, IEnumerable<(string Label, string Value)> rows)
	{
		container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
		{
			column.Item().Text(title).Bold().FontSize(12).FontColor("#0F172A");
			column.Item().PaddingTop(6).Table(table =>
			{
				table.ColumnsDefinition(columns => { columns.ConstantColumn(130); columns.RelativeColumn(); });
				foreach (var row in rows)
				{
					table.Cell().PaddingVertical(2).Text(row.Label).SemiBold();
					table.Cell().PaddingVertical(2).Text(row.Value);
				}
			});
		});
	}

	private static void ComposePdfSimpleList(IContainer container, string title, IEnumerable<string> rows)
	{
		var items = rows.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
		container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
		{
			column.Item().Text(title).Bold().FontSize(12).FontColor("#0F172A");
			if (items.Count == 0)
			{
				column.Item().PaddingTop(4).Text("-");
				return;
			}
			foreach (var item in items) column.Item().PaddingTop(4).Text(item);
		});
	}

	private static List<PerformanceTemplateRequest> GetDefaultTemplateRequests() =>
	[
		new()
		{
			Name = "Teacher Appraisal Template",
			StaffCategory = PerformanceStaffCategory.Teacher,
			Description = "Indian school teacher appraisal covering classroom practice, learning outcomes, conduct, and school contribution.",
			Sections =
			[
				DefaultSection("Lesson planning and syllabus completion", 15, 1, ["Lesson plans are timely and aligned", "Syllabus progress is maintained"]),
				DefaultSection("Classroom delivery and student engagement", 20, 2, ["Explains concepts clearly", "Uses inclusive engagement techniques"]),
				DefaultSection("Learning outcomes and assessment quality", 20, 3, ["Improves student outcomes", "Uses fair assessment and correction practices"]),
				DefaultSection("Discipline, parent communication and conduct", 20, 4, ["Maintains classroom discipline", "Communicates constructively with parents"]),
				DefaultSection("School contribution and professional growth", 25, 5, ["Participates in school events", "Completes training and improvement actions"])
			]
		},
		new()
		{
			Name = "Non-Teaching Staff Appraisal Template",
			StaffCategory = PerformanceStaffCategory.Admin,
			Description = "Indian school support staff appraisal covering reliability, quality, communication, compliance, and teamwork.",
			Sections =
			[
				DefaultSection("Attendance and punctuality", 20, 1, ["Maintains reliable attendance", "Reports on time"]),
				DefaultSection("Task completion and quality", 30, 2, ["Completes assigned work", "Maintains accuracy"]),
				DefaultSection("Communication and stakeholder handling", 20, 3, ["Handles parents/students professionally", "Coordinates with team members"]),
				DefaultSection("Process compliance and initiative", 30, 4, ["Follows school processes", "Takes ownership and improves work"])
			]
		}
	];

	private static PerformanceTemplateSectionRequest DefaultSection(string name, decimal weight, int order, IReadOnlyList<string> criteria) => new()
	{
		Name = name,
		Weight = weight,
		DisplayOrder = order,
		Criteria = criteria.Select((criterion, index) => new PerformanceTemplateCriterionRequest { Name = criterion, Weight = Math.Round(100m / criteria.Count, 2), DisplayOrder = index + 1, EvidenceRequired = false }).ToList()
	};
}
