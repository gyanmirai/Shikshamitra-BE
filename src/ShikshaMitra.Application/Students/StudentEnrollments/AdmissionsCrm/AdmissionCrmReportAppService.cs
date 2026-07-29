using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmReportAppService : ApplicationService, IAdmissionCrmReportAppService
{
	private const int MaxReportWindowDays = 366;

	private static readonly AdmissionCrmPipelineColumnKey[] LifecycleColumns =
	[
		AdmissionCrmPipelineColumnKey.NewInquiry,
		AdmissionCrmPipelineColumnKey.FollowUp,
		AdmissionCrmPipelineColumnKey.ExamPending,
		AdmissionCrmPipelineColumnKey.ExamDone,
		AdmissionCrmPipelineColumnKey.Eligible,
		AdmissionCrmPipelineColumnKey.RegistrationPending,
		AdmissionCrmPipelineColumnKey.RegistrationDone,
		AdmissionCrmPipelineColumnKey.FinanceVerification,
		AdmissionCrmPipelineColumnKey.PlacementPending,
		AdmissionCrmPipelineColumnKey.ReadyForActivation,
		AdmissionCrmPipelineColumnKey.Activated
	];

	private static readonly AdmissionCrmClosedOutcome[] ClosedOutcomes =
	[
		AdmissionCrmClosedOutcome.Rejected,
		AdmissionCrmClosedOutcome.Withdrawn,
		AdmissionCrmClosedOutcome.Ineligible,
		AdmissionCrmClosedOutcome.Cancelled,
		AdmissionCrmClosedOutcome.Duplicate,
		AdmissionCrmClosedOutcome.NotInterested
	];

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly AdmissionCrmPipelineQueryService _pipelineQueryService;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmReportAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<Standard, Guid> standardRepository,
		AdmissionCrmPipelineQueryService pipelineQueryService,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_standardRepository = standardRepository;
		_pipelineQueryService = pipelineQueryService;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics)]
	public async Task<AdmissionCrmAdmissionReportDto> GetAdmissionReportAsync(
		Guid academicYearId,
		GetAdmissionCrmAdmissionReportInput input)
	{
		input ??= new GetAdmissionCrmAdmissionReportInput();
		ValidateInput(input);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetAdmissionReportAsync));

		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var leads = leadQueryable.Where(lead =>
			lead.TenantId == CurrentTenant.Id &&
			lead.AcademicYearId == academicYearId);
		leads = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(leads);
		if (input.FromDate.HasValue)
		{
			var from = input.FromDate.Value.Date;
			leads = leads.Where(lead => lead.CreationTime >= from);
		}
		if (input.ToDate.HasValue)
		{
			var until = input.ToDate.Value.Date.AddDays(1);
			leads = leads.Where(lead => lead.CreationTime < until);
		}
		if (input.TargetStandardId.HasValue)
		{
			leads = leads.Where(lead => lead.TargetStandardId == input.TargetStandardId.Value);
		}

		var rows = await _pipelineQueryService.BuildReportRowsAsync(leads, CurrentTenant.Id);
		var lifecycleCounts = await AsyncExecuter.ToListAsync(
			rows
				.Where(row => row.Column.HasValue)
				.GroupBy(row => row.Column)
				.Select(group => new
				{
					Column = group.Key,
					Count = group.Count()
				}));
		var outcomeCounts = await AsyncExecuter.ToListAsync(
			rows
				.Where(row => row.ClosedOutcome.HasValue)
				.GroupBy(row => row.ClosedOutcome)
				.Select(group => new
				{
					Outcome = group.Key,
					Count = group.Count()
				}));
		var sourceCounts = await AsyncExecuter.ToListAsync(
			rows
				.GroupBy(row =>
					row.InquirySourceName == null || row.InquirySourceName == string.Empty
						? "Unspecified"
						: row.InquirySourceName)
				.Select(group => new
				{
					SourceName = group.Key,
					InquiryCount = group.Count(),
					ExamScheduledCount = group.Count(row => row.ExamScheduled),
					ActivatedCount = group.Count(row =>
						row.Column == AdmissionCrmPipelineColumnKey.Activated)
				}));
		var standardCounts = await AsyncExecuter.ToListAsync(
			rows
				.GroupBy(row => row.TargetStandardId)
				.Select(group => new
				{
					TargetStandardId = group.Key,
					InquiryCount = group.Count(),
					ExamScheduledCount = group.Count(row => row.ExamScheduled),
					ExamCompletedCount = group.Count(row => row.ExamCompleted),
					EligibleCount = group.Count(row => row.Eligible),
					RegistrationPendingCount = group.Count(row =>
						row.Column == AdmissionCrmPipelineColumnKey.RegistrationPending),
					RegistrationVerifiedCount = group.Count(row => row.RegistrationVerified),
					FinancePendingCount = group.Count(row =>
						row.Column == AdmissionCrmPipelineColumnKey.FinanceVerification),
					ActivationPendingCount = group.Count(row =>
						row.Column == AdmissionCrmPipelineColumnKey.PlacementPending ||
						row.Column == AdmissionCrmPipelineColumnKey.ReadyForActivation),
					ActivatedCount = group.Count(row =>
						row.Column == AdmissionCrmPipelineColumnKey.Activated),
					RejectedCount = group.Count(row =>
						row.ClosedOutcome == AdmissionCrmClosedOutcome.Rejected ||
						row.ClosedOutcome == AdmissionCrmClosedOutcome.Ineligible),
					WithdrawnCount = group.Count(row =>
						row.ClosedOutcome == AdmissionCrmClosedOutcome.Withdrawn)
				}));

		var standardIds = standardCounts
			.Where(row => row.TargetStandardId.HasValue)
			.Select(row => row.TargetStandardId!.Value)
			.Distinct()
			.ToList();
		var standardNames = standardIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await _standardRepository.GetListAsync(standard =>
					standard.TenantId == CurrentTenant.Id &&
					standardIds.Contains(standard.Id)))
				.ToDictionary(standard => standard.Id, standard => standard.Name);

		var lifecycleCountByColumn = lifecycleCounts
			.Where(row => row.Column.HasValue)
			.ToDictionary(row => row.Column!.Value, row => row.Count);
		var outcomeCountByType = outcomeCounts
			.Where(row => row.Outcome.HasValue)
			.ToDictionary(row => row.Outcome!.Value, row => row.Count);
		var openApplicantCount = lifecycleCountByColumn.Values.Sum();
		var closedApplicantCount = outcomeCountByType.Values.Sum();
		var totalApplicantCount = openApplicantCount + closedApplicantCount;
		var activatedApplicantCount =
			lifecycleCountByColumn.GetValueOrDefault(AdmissionCrmPipelineColumnKey.Activated);
		var rejectedApplicantCount =
			outcomeCountByType.GetValueOrDefault(AdmissionCrmClosedOutcome.Rejected) +
			outcomeCountByType.GetValueOrDefault(AdmissionCrmClosedOutcome.Ineligible);
		var withdrawnApplicantCount =
			outcomeCountByType.GetValueOrDefault(AdmissionCrmClosedOutcome.Withdrawn);

		return new AdmissionCrmAdmissionReportDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = Clock.Now,
			FromDate = input.FromDate?.Date,
			ToDate = input.ToDate?.Date,
			TargetStandardId = input.TargetStandardId,
			IsScopeRestricted =
				!await _studentScopeAccessService.CurrentUserHasAcademicYearWideScopeAsync(academicYearId),
			TotalApplicantCount = totalApplicantCount,
			OpenApplicantCount = openApplicantCount - activatedApplicantCount,
			ActivatedApplicantCount = activatedApplicantCount,
			RejectedApplicantCount = rejectedApplicantCount,
			WithdrawnApplicantCount = withdrawnApplicantCount,
			UnassignedStandardApplicantCount = standardCounts
				.Where(row => !row.TargetStandardId.HasValue)
				.Sum(row => row.InquiryCount),
			Lifecycle = LifecycleColumns
				.Select(column => new AdmissionCrmAdmissionReportLifecycleDto
				{
					Column = column,
					ColumnName = GetColumnName(column),
					ApplicantCount = lifecycleCountByColumn.GetValueOrDefault(column),
					ShareOfApplicants = Rate(
						lifecycleCountByColumn.GetValueOrDefault(column),
						totalApplicantCount)
				})
				.ToList(),
			Sources = sourceCounts
				.OrderByDescending(row => row.InquiryCount)
				.ThenBy(row => row.SourceName)
				.Select(row => new AdmissionCrmAdmissionReportSourceDto
				{
					SourceName = row.SourceName,
					InquiryCount = row.InquiryCount,
					ExamScheduledCount = row.ExamScheduledCount,
					ActivatedCount = row.ActivatedCount,
					ActivationRate = Rate(row.ActivatedCount, row.InquiryCount)
				})
				.ToList(),
			Standards = standardCounts
				.OrderBy(row => row.TargetStandardId.HasValue ? 0 : 1)
				.ThenBy(row =>
					row.TargetStandardId.HasValue &&
					standardNames.TryGetValue(row.TargetStandardId.Value, out var name)
						? name
						: "Not selected")
				.Select(row => new AdmissionCrmAdmissionReportStandardDto
				{
					TargetStandardId = row.TargetStandardId,
					StandardName =
						row.TargetStandardId.HasValue &&
						standardNames.TryGetValue(row.TargetStandardId.Value, out var name)
							? name
							: "Not selected",
					InquiryCount = row.InquiryCount,
					ExamScheduledCount = row.ExamScheduledCount,
					ExamCompletedCount = row.ExamCompletedCount,
					EligibleCount = row.EligibleCount,
					RegistrationPendingCount = row.RegistrationPendingCount,
					RegistrationVerifiedCount = row.RegistrationVerifiedCount,
					FinancePendingCount = row.FinancePendingCount,
					ActivationPendingCount = row.ActivationPendingCount,
					ActivatedCount = row.ActivatedCount,
					RejectedCount = row.RejectedCount,
					WithdrawnCount = row.WithdrawnCount
				})
				.ToList(),
			Outcomes = ClosedOutcomes
				.Select(outcome => new AdmissionCrmAdmissionReportOutcomeDto
				{
					Outcome = outcome,
					OutcomeName = GetOutcomeName(outcome),
					ApplicantCount = outcomeCountByType.GetValueOrDefault(outcome),
					ShareOfApplicants = Rate(
						outcomeCountByType.GetValueOrDefault(outcome),
						totalApplicantCount)
				})
				.ToList()
		};
	}

	private static void ValidateInput(GetAdmissionCrmAdmissionReportInput input)
	{
		if (input.FromDate.HasValue &&
			input.ToDate.HasValue &&
			input.FromDate.Value.Date > input.ToDate.Value.Date)
		{
			throw new BusinessException("AdmissionCrm:ReportInvalidDateRange");
		}
		if (input.FromDate.HasValue &&
			input.ToDate.HasValue &&
			(input.ToDate.Value.Date - input.FromDate.Value.Date).TotalDays > MaxReportWindowDays)
		{
			throw new BusinessException("AdmissionCrm:ReportDateRangeTooLarge")
				.WithData("MaxDays", MaxReportWindowDays);
		}
	}

	private static decimal Rate(int numerator, int denominator) =>
		denominator <= 0
			? 0
			: Math.Round(numerator * 100m / denominator, 2);

	private static string GetColumnName(AdmissionCrmPipelineColumnKey column) =>
		column switch
		{
			AdmissionCrmPipelineColumnKey.NewInquiry => "New Inquiry",
			AdmissionCrmPipelineColumnKey.FollowUp => "Follow-up",
			AdmissionCrmPipelineColumnKey.ExamPending => "Exam Pending",
			AdmissionCrmPipelineColumnKey.ExamDone => "Exam Done",
			AdmissionCrmPipelineColumnKey.Eligible => "Eligible",
			AdmissionCrmPipelineColumnKey.RegistrationPending => "Registration Pending",
			AdmissionCrmPipelineColumnKey.RegistrationDone => "Registration Done",
			AdmissionCrmPipelineColumnKey.FinanceVerification => "Finance Verification",
			AdmissionCrmPipelineColumnKey.PlacementPending => "Placement Pending",
			AdmissionCrmPipelineColumnKey.ReadyForActivation => "Ready for Activation",
			AdmissionCrmPipelineColumnKey.Activated => "Activated",
			_ => column.ToString()
		};

	private static string GetOutcomeName(AdmissionCrmClosedOutcome outcome) =>
		outcome switch
		{
			AdmissionCrmClosedOutcome.Rejected => "Rejected",
			AdmissionCrmClosedOutcome.Withdrawn => "Withdrawn",
			AdmissionCrmClosedOutcome.Ineligible => "Ineligible",
			AdmissionCrmClosedOutcome.Cancelled => "Cancelled",
			AdmissionCrmClosedOutcome.Duplicate => "Duplicate",
			AdmissionCrmClosedOutcome.NotInterested => "Not Interested",
			_ => outcome.ToString()
		};
}
