using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmStructuredReviewAppService : ApplicationService, IAdmissionCrmStructuredReviewAppService
{
	private static readonly IReadOnlyCollection<AdmissionCrmReviewCriterionInput> DefaultCriteria =
	[
		new("readiness", "School readiness", 5, true),
		new("academic_fit", "Academic fit", 5, true),
		new("support_needs", "Support needs understood", 5, true)
	];

	private readonly IRepository<AdmissionCrmReviewConfiguration, Guid> _configurationRepository;
	private readonly IRepository<AdmissionCrmStructuredReview, Guid> _reviewRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _scopeAccess;
	private readonly AdmissionCrmStaffEligibilityService _staffEligibility;

	public AdmissionCrmStructuredReviewAppService(
		IRepository<AdmissionCrmReviewConfiguration, Guid> configurationRepository,
		IRepository<AdmissionCrmStructuredReview, Guid> reviewRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService scopeAccess,
		AdmissionCrmStaffEligibilityService staffEligibility)
	{
		_configurationRepository = configurationRepository;
		_reviewRepository = reviewRepository;
		_leadRepository = leadRepository;
		_timelineRepository = timelineRepository;
		_scopeAccess = scopeAccess;
		_staffEligibility = staffEligibility;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewStructuredReviews)]
	public async Task<AdmissionCrmReviewConfigurationDto> GetConfigurationAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetConfigurationAsync));
		var configuration = await GetConfigurationEntityAsync(academicYearId);
		return configuration == null ? CreateDefaultConfigurationDto(academicYearId) : MapConfiguration(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageReviewConfiguration)]
	public async Task<AdmissionCrmReviewConfigurationDto> UpdateConfigurationAsync(
		Guid academicYearId, UpdateAdmissionCrmReviewConfigurationDto input)
	{
		await _scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, nameof(UpdateConfigurationAsync));
		var configuration = await GetConfigurationEntityAsync(academicYearId);
		var isNew = configuration == null;
		configuration ??= new AdmissionCrmReviewConfiguration(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
		configuration.Update(input.IsEnabled, input.Title, input.RequiredCompletedReviews,
			input.RequireConflictDeclaration, input.Criteria.Select(x => new AdmissionCrmReviewCriterionInput(
				x.Key, x.Label, x.MaximumScore, x.IsRequired)).ToList(), GuidGenerator.Create);
		if (isNew) await _configurationRepository.InsertAsync(configuration, autoSave: true);
		else await _configurationRepository.UpdateAsync(configuration, autoSave: true);
		return MapConfiguration(configuration);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewStructuredReviews)]
	public async Task<AdmissionCrmStructuredReviewDto?> GetByLeadAsync(Guid leadId)
	{
		var lead = await GetScopedLeadAsync(leadId, nameof(GetByLeadAsync));
		var review = await GetReviewByLeadAsync(lead.Id);
		return review == null ? null : await MapReviewAsync(review);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews)]
	public async Task<IReadOnlyList<AdmissionCrmReviewAssignmentDto>> GetMyAssignmentsAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetMyAssignmentsAsync));
		var userId = GetCurrentUserId();
		var query = (await _reviewRepository.WithDetailsAsync(x => x.Assignments, x => x.Criteria))
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
				x.Assignments.Any(a => a.ReviewerUserId == userId));
		var reviews = await AsyncExecuter.ToListAsync(query);
		return reviews.SelectMany(x => x.Assignments.Where(a => a.ReviewerUserId == userId)
			.Select(a => MapAssignment(a, true, x))).OrderBy(x => x.DueAt).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignReviewers)]
	public async Task<AdmissionCrmStructuredReviewDto> StartAsync(Guid leadId)
	{
		var lead = await GetScopedLeadAsync(leadId, nameof(StartAsync));
		var existing = await GetReviewByLeadAsync(lead.Id);
		if (existing != null) return await MapReviewAsync(existing);
		var configuration = await GetConfigurationEntityAsync(lead.AcademicYearId)
			?? throw new BusinessException("AdmissionCrm:StructuredReviewDisabled");
		var review = new AdmissionCrmStructuredReview(GuidGenerator.Create(), CurrentTenant.Id,
			lead.AcademicYearId, lead.Id, configuration, Clock.Now, GuidGenerator.Create);
		await _reviewRepository.InsertAsync(review, autoSave: false);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.StructuredReviewStarted,
			"Structured admissions review started", configuration.Title, review.Id);
		await CurrentUnitOfWork.SaveChangesAsync();
		return await MapReviewAsync(review);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignReviewers)]
	public async Task<AdmissionCrmStructuredReviewDto> AssignAsync(Guid reviewId, AssignAdmissionCrmReviewerDto input)
	{
		var review = await GetScopedReviewAsync(reviewId, nameof(AssignAsync));
		await _staffEligibility.EnsureReviewerEligibleAsync(input.ReviewerUserId);
		var assignment = review.Assign(GuidGenerator.Create(), input.ReviewerUserId, input.DueAt, Clock.Now);
		var lead = await GetScopedLeadAsync(review.LeadId, nameof(AssignAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.StructuredReviewerAssigned,
			"Admissions reviewer assigned", $"Review due {input.DueAt:u}", assignment.Id);
		await _reviewRepository.UpdateAsync(review, autoSave: true);
		return await MapReviewAsync(review);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews)]
	public async Task<AdmissionCrmReviewAssignmentDto> DeclareConflictAsync(
		Guid assignmentId, DeclareAdmissionCrmReviewConflictDto input)
	{
		var review = await GetReviewForCurrentAssignmentAsync(assignmentId, nameof(DeclareConflictAsync));
		review.DeclareConflict(assignmentId, GetCurrentUserId(), input.HasConflict, input.Reason, Clock.Now);
		var lead = await GetScopedLeadAsync(review.LeadId, nameof(DeclareConflictAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.StructuredReviewConflictDeclared,
			"Reviewer conflict declaration recorded",
			input.HasConflict ? "Reviewer recused due to a declared conflict." : "Reviewer declared no conflict.", assignmentId);
		await _reviewRepository.UpdateAsync(review, autoSave: true);
		return MapAssignment(review.Assignments.Single(x => x.Id == assignmentId), true, review);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews)]
	public async Task<AdmissionCrmReviewAssignmentDto> SubmitAsync(
		Guid assignmentId, SubmitAdmissionCrmStructuredReviewDto input)
	{
		var review = await GetReviewForCurrentAssignmentAsync(assignmentId, nameof(SubmitAsync));
		review.Submit(assignmentId, GetCurrentUserId(), input.Recommendation,
			input.ConfidentialRecommendation, input.Scores, Clock.Now);
		var lead = await GetScopedLeadAsync(review.LeadId, nameof(SubmitAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.StructuredReviewSubmitted,
			"Confidential admissions review submitted",
			$"{review.SubmittedReviewCount} of {review.RequiredCompletedReviews} required reviews submitted.", assignmentId);
		await _reviewRepository.UpdateAsync(review, autoSave: true);
		return MapAssignment(review.Assignments.Single(x => x.Id == assignmentId), true, review);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MakeAdmissionDecision)]
	public async Task<AdmissionCrmStructuredReviewDto> CloseAsync(Guid reviewId, CloseAdmissionCrmStructuredReviewDto input)
	{
		var review = await GetScopedReviewAsync(reviewId, nameof(CloseAsync));
		review.Close(GetCurrentUserId(), Clock.Now, input.Reason);
		var lead = await GetScopedLeadAsync(review.LeadId, nameof(CloseAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.StructuredReviewClosed,
			"Structured admissions review closed", input.Reason, review.Id);
		await _reviewRepository.UpdateAsync(review, autoSave: true);
		return await MapReviewAsync(review);
	}

	private async Task<AdmissionCrmReviewConfiguration?> GetConfigurationEntityAsync(Guid academicYearId)
	{
		var query = await _configurationRepository.WithDetailsAsync(x => x.Criteria);
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
	}

	private async Task<AdmissionCrmStructuredReview?> GetReviewByLeadAsync(Guid leadId)
	{
		var query = await _reviewRepository.WithDetailsAsync(x => x.Criteria, x => x.Assignments);
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	private async Task<AdmissionCrmStructuredReview> GetScopedReviewAsync(Guid reviewId, string operation)
	{
		var query = await _reviewRepository.WithDetailsAsync(x => x.Criteria, x => x.Assignments);
		var review = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id && x.Id == reviewId))
			?? throw new BusinessException("AdmissionCrm:StructuredReviewNotFound");
		await GetScopedLeadAsync(review.LeadId, operation);
		return review;
	}

	private async Task<AdmissionCrmStructuredReview> GetReviewForCurrentAssignmentAsync(Guid assignmentId, string operation)
	{
		var userId = GetCurrentUserId();
		var query = await _reviewRepository.WithDetailsAsync(x => x.Criteria, x => x.Assignments);
		var review = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			x.Assignments.Any(a => a.Id == assignmentId && a.ReviewerUserId == userId)))
			?? throw new BusinessException("AdmissionCrm:ReviewAssignmentNotFound");
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(review.AcademicYearId, operation);
		return review;
	}

	private async Task<AdmissionInquiryLead> GetScopedLeadAsync(Guid leadId, string operation)
	{
		var query = (await _leadRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.Id == leadId);
		query = await _scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		var lead = await AsyncExecuter.FirstOrDefaultAsync(query)
			?? throw new BusinessException("AdmissionCrm:LeadNotFound").WithData("LeadId", leadId);
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation);
		return lead;
	}

	private async Task<AdmissionCrmStructuredReviewDto> MapReviewAsync(AdmissionCrmStructuredReview review)
	{
		var currentUserId = CurrentUser.Id;
		var canViewAll = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewConfidentialReviews);
		return new AdmissionCrmStructuredReviewDto
		{
			Id = review.Id, AcademicYearId = review.AcademicYearId, LeadId = review.LeadId,
			ConfigurationVersion = review.ConfigurationVersion, Title = review.Title,
			RequiredCompletedReviews = review.RequiredCompletedReviews,
			SubmittedReviewCount = review.SubmittedReviewCount,
			RequireConflictDeclaration = review.RequireConflictDeclaration, Status = review.Status,
			StartedAt = review.StartedAt, ReadyAt = review.ReadyAt, ClosedAt = review.ClosedAt,
			ClosureReason = review.ClosureReason,
			Criteria = review.Criteria.OrderBy(x => x.DisplayOrder).Select(MapCriterion).ToList(),
			Assignments = review.Assignments.OrderBy(x => x.DueAt)
				.Select(x => MapAssignment(x, canViewAll || x.ReviewerUserId == currentUserId, review)).ToList()
		};
	}

	private static AdmissionCrmReviewConfigurationDto MapConfiguration(AdmissionCrmReviewConfiguration configuration) => new()
	{
		Id = configuration.Id, AcademicYearId = configuration.AcademicYearId, IsEnabled = configuration.IsEnabled,
		Title = configuration.Title, RequiredCompletedReviews = configuration.RequiredCompletedReviews,
		RequireConflictDeclaration = configuration.RequireConflictDeclaration, Version = configuration.Version,
		Criteria = configuration.Criteria.OrderBy(x => x.DisplayOrder).Select(MapCriterion).ToList()
	};

	private static AdmissionCrmReviewConfigurationDto CreateDefaultConfigurationDto(Guid academicYearId) => new()
	{
		AcademicYearId = academicYearId, IsEnabled = false, Title = "Admissions committee review",
		RequiredCompletedReviews = 2, RequireConflictDeclaration = true, Version = 0,
		Criteria = DefaultCriteria.Select((x, index) => new AdmissionCrmReviewCriterionDto
		{
			Key = x.Key, Label = x.Label, MaximumScore = x.MaximumScore,
			IsRequired = x.IsRequired, DisplayOrder = index
		}).ToList()
	};

	private static AdmissionCrmReviewCriterionDto MapCriterion(AdmissionCrmReviewConfigurationCriterion criterion) => new()
	{
		Id = criterion.Id, Key = criterion.Key, Label = criterion.Label,
		MaximumScore = criterion.MaximumScore, IsRequired = criterion.IsRequired, DisplayOrder = criterion.DisplayOrder
	};

	private static AdmissionCrmReviewCriterionDto MapCriterion(AdmissionCrmStructuredReviewCriterion criterion) => new()
	{
		Id = criterion.Id, Key = criterion.Key, Label = criterion.Label,
		MaximumScore = criterion.MaximumScore, IsRequired = criterion.IsRequired, DisplayOrder = criterion.DisplayOrder
	};

	private AdmissionCrmReviewAssignmentDto MapAssignment(AdmissionCrmReviewAssignment assignment,
		bool includeConfidential, AdmissionCrmStructuredReview review)
	{
		Dictionary<string, int?>? scores = null;
		if (includeConfidential && !string.IsNullOrWhiteSpace(assignment.ScoresJson))
		{
			try { scores = JsonSerializer.Deserialize<Dictionary<string, int?>>(assignment.ScoresJson); }
			catch (JsonException) { scores = []; }
		}
		return new AdmissionCrmReviewAssignmentDto
		{
			Id = assignment.Id, ReviewId = assignment.ReviewId, LeadId = review.LeadId,
			ReviewTitle = review.Title, ReviewStatus = review.Status,
			ReviewerUserId = assignment.ReviewerUserId,
			IsCurrentReviewer = assignment.ReviewerUserId == CurrentUser.Id, DueAt = assignment.DueAt,
			Status = assignment.Status, AssignedAt = assignment.AssignedAt,
			HasDeclaredConflict = includeConfidential ? assignment.HasDeclaredConflict : null,
			ConflictReason = includeConfidential ? assignment.ConflictReason : null,
			ConflictDeclaredAt = includeConfidential ? assignment.ConflictDeclaredAt : null,
			Recommendation = includeConfidential ? assignment.Recommendation : null,
			ConfidentialRecommendation = includeConfidential ? assignment.ConfidentialRecommendation : null,
			Scores = scores, SubmittedAt = assignment.SubmittedAt,
			Criteria = review.Criteria.OrderBy(x => x.DisplayOrder).Select(MapCriterion).ToList()
		};
	}

	private Guid GetCurrentUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user is required.");

	private async Task AddTimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type,
		string title, string details, Guid relatedId)
	{
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id,
			lead.Id, lead.AcademicYearId, type, title, details, relatedId, CurrentUser.Id,
			new DateTimeOffset(Clock.Now)), autoSave: false);
	}
}
