using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEntranceExamAppService : ApplicationService, IAdmissionCrmEntranceExamAppService
{
	private const AdmissionCrmExamReadinessField DefaultExamReadinessFields =
		AdmissionCrmExamReadinessField.Contact |
		AdmissionCrmExamReadinessField.AdmissionIntent |
		AdmissionCrmExamReadinessField.DesiredClass;

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<StudentAdditionalDetail, Guid> _studentAdditionalDetailRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IAdmissionCrmLeadAppService _leadAppService;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmEntranceExamAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IAdmissionCrmLeadAppService leadAppService,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_examRepository = examRepository;
		_slotRepository = slotRepository;
		_studentAdditionalDetailRepository = studentAdditionalDetailRepository;
		_timelineRepository = timelineRepository;
		_cycleRepository = cycleRepository;
		_leadAppService = leadAppService;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam)]
	public async Task<AdmissionCrmEntranceExamDto> ScheduleAsync(
		Guid leadId,
		ScheduleAdmissionCrmEntranceExamDto input)
	{
		return await ScheduleCoreAsync(leadId, input, enforceUserScope: true);
	}

	[AllowAnonymous]
	public async Task<AdmissionCrmEntranceExamDto> ScheduleFromApplicantAsync(
		Guid leadId,
		Guid entranceExamSlotId)
	{
		return await ScheduleCoreAsync(
			leadId,
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = entranceExamSlotId },
			enforceUserScope: false);
	}

	private async Task<AdmissionCrmEntranceExamDto> ScheduleCoreAsync(
		Guid leadId,
		ScheduleAdmissionCrmEntranceExamDto input,
		bool enforceUserScope)
	{
		var lead = await GetLeadForCurrentTenantAsync(leadId, nameof(ScheduleAsync));
		var cycleConfiguration = await GetCycleConfigurationOrDefaultAsync(lead.AcademicYearId);
		await EnsureRegistrationFormReadyAsync(
			lead,
			cycleConfiguration?.RegistrationFormRequiredBeforeExam == true);
		EnsureExamReady(lead, cycleConfiguration?.ExamReadinessFields ?? DefaultExamReadinessFields);

		var slot = await _slotRepository.GetAsync(input.EntranceExamSlotId);
		if (slot.TenantId != CurrentTenant.Id || slot.AcademicYearId != lead.AcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:ExamSlotNotFound")
				.WithData("EntranceExamSlotId", input.EntranceExamSlotId);
		}

		if (enforceUserScope)
		{
			await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(slot.AcademicYearId, nameof(ScheduleAsync));
		}

		if (slot.StartDate <= DateTimeOffset.UtcNow)
		{
			throw new BusinessException("AdmissionCrm:ExamSlotAlreadyStarted")
				.WithData("EntranceExamSlotId", slot.Id);
		}

		if (!enforceUserScope && input.OverrideCapacity)
		{
			throw new BusinessException("AdmissionCrm:ApplicantCapacityOverrideNotAllowed");
		}
		if (input.OverrideCapacity)
		{
			await EnsureOverrideCapacityPermissionAsync();
		}

		var existing = await GetExamByLeadOrDefaultAsync(lead.Id);
		var isReschedule = existing != null;
		await EnsureSlotCapacityAsync(slot, input.OverrideCapacity, existing?.Id);

		var now = Clock.Now;
		var previousStage = lead.Stage;
		if (existing == null)
		{
			existing = new AdmissionCrmEntranceExamRecord(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				lead.AcademicYearId,
				lead.Id,
				slot,
				input.OverrideCapacity,
				input.CapacityOverrideReason,
				now);

			await _examRepository.InsertAsync(existing, autoSave: false);
		}
		else
		{
			existing.Reschedule(slot, input.OverrideCapacity, input.CapacityOverrideReason, now);
			await _examRepository.UpdateAsync(existing, autoSave: false);
		}

		lead.ChangeStage(AdmissionInquiryStage.ExamScheduled);
		await AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.ExamScheduled,
			isReschedule ? "Entrance exam rescheduled" : "Entrance exam scheduled",
			$"{slot.SlotName} in room {slot.RoomNo}",
			existing.Id);
		await _leadRepository.UpdateAsync(lead, autoSave: true);
		await RunJourneyAutomationAsync(lead, AdmissionCrmJourneyTrigger.ExamScheduled);
		await RunStageReachedJourneyIfChangedAsync(lead, previousStage);
		return ToDto(existing, lead);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmEntranceExamDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmEntranceExamListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetListAsync));

		var queryable = await _examRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);

		if (input.AttendanceStatus.HasValue)
		{
			query = query.Where(x => x.AttendanceStatus == input.AttendanceStatus.Value);
		}

		if (input.ExamStatus.HasValue)
		{
			query = query.Where(x => x.ExamStatus == input.ExamStatus.Value);
		}

		if (input.EligibilityDecision.HasValue)
		{
			query = query.Where(x => x.EligibilityDecision == input.EligibilityDecision.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var exams = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.ExamStartTime)
				.ThenBy(x => x.LeadId)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));
		var leadIds = exams.Select(x => x.LeadId).Distinct().ToList();
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var leads = await AsyncExecuter.ToListAsync(
			leadQueryable.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == academicYearId &&
				leadIds.Contains(x.Id)));
		var leadsById = leads.ToDictionary(x => x.Id);

		return new PagedResultDto<AdmissionCrmEntranceExamDto>(
			totalCount,
			exams.Select(exam => ToDto(exam, leadsById.GetValueOrDefault(exam.LeadId))).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmEntranceExamDto> GetByLeadAsync(Guid leadId)
	{
		var lead = await GetLeadForCurrentTenantAsync(leadId, nameof(GetByLeadAsync));
		var exam = await GetExamByLeadOrDefaultAsync(lead.Id)
		           ?? throw new BusinessException("AdmissionCrm:ExamNotScheduled")
			           .WithData("LeadId", leadId);

		return ToDto(exam, lead);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmEligibilityHandoffDto> GetEligibilityHandoffAsync(Guid examRecordId)
	{
		var exam = await GetExamForCurrentTenantAsync(examRecordId, nameof(GetEligibilityHandoffAsync));
		var lead = await GetLeadForCurrentTenantAsync(exam.LeadId, nameof(GetEligibilityHandoffAsync));

		return ToHandoffDto(lead, exam);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmEntranceExamDto> MarkAttendanceAsync(
		Guid examRecordId,
		MarkAdmissionCrmExamAttendanceDto input)
	{
		var exam = await GetExamForCurrentTenantAsync(examRecordId, nameof(MarkAttendanceAsync));
		var lead = await GetLeadForCurrentTenantAsync(exam.LeadId, nameof(MarkAttendanceAsync));

		exam.MarkAttendance(input.AttendanceStatus, Clock.Now);
		ApplyLeadStageFromExam(lead, exam);
		await _examRepository.UpdateAsync(exam, autoSave: false);
		await AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.ExamAttendanceMarked,
			"Exam attendance marked",
			input.AttendanceStatus.ToString(),
			exam.Id);
		await _leadRepository.UpdateAsync(lead, autoSave: true);
		return ToDto(exam, lead);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmEntranceExamDto> UploadResultAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input)
	{
		var exam = await GetExamForCurrentTenantAsync(examRecordId, nameof(UploadResultAsync));
		var lead = await GetLeadForCurrentTenantAsync(exam.LeadId, nameof(UploadResultAsync));
		var cycleConfiguration = await GetCycleConfigurationOrDefaultAsync(lead.AcademicYearId);
		var previousStage = lead.Stage;

		exam.RecordResult(
			input.TotalMarks,
			input.PassingMarks,
			input.ObtainedMarks,
			input.MeritRank,
			Clock.Now);
		if (!input.MeritRank.HasValue)
		{
			await RecalculateClassWiseMeritRanksAsync(
				lead,
				exam,
				cycleConfiguration?.MeritRankingMode ?? AdmissionCrmMeritRankingMode.MarksThenInquiryDate);
		}

		lead.UpdateScore(AdmissionCrmLeadScoreCalculator.Calculate(
			lead,
			AdmissionCrmLeadScoreCalculator.DeserializeRules(cycleConfiguration?.LeadScoringRulesJson),
			exam));
		ApplyLeadStageFromExam(lead, exam);
		await _examRepository.UpdateAsync(exam, autoSave: false);
		await AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.ExamResultRecorded,
			"Exam result recorded",
			$"{input.ObtainedMarks}/{input.TotalMarks}",
			exam.Id);
		await _leadRepository.UpdateAsync(lead, autoSave: true);
		await RunJourneyAutomationAsync(lead, AdmissionCrmJourneyTrigger.ExamCompleted);
		await RunStageReachedJourneyIfChangedAsync(lead, previousStage);
		return ToDto(exam, lead);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmEntranceExamDto> DecideEligibilityAsync(
		Guid examRecordId,
		UpdateAdmissionCrmEligibilityDecisionDto input)
	{
		var exam = await GetExamForCurrentTenantAsync(examRecordId, nameof(DecideEligibilityAsync));
		var lead = await GetLeadForCurrentTenantAsync(exam.LeadId, nameof(DecideEligibilityAsync));
		var previousStage = lead.Stage;

		if (input.OverrideDecision)
		{
			await EnsureEligibilityOverridePermissionAsync();
		}

		exam.SetEligibilityDecision(
			input.Decision,
			input.Reason,
			input.OverrideDecision,
			input.OverrideReason,
			input.MeritRank,
			Clock.Now);
		ApplyLeadStageFromExam(lead, exam);
		await _examRepository.UpdateAsync(exam, autoSave: false);
		await AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.EligibilityDecided,
			"Eligibility decision updated",
			$"{input.Decision}: {input.Reason}",
			exam.Id);
		await _leadRepository.UpdateAsync(lead, autoSave: true);
		await RunJourneyAutomationAsync(lead, AdmissionCrmJourneyTrigger.EligibilityDecided);
		await RunStageReachedJourneyIfChangedAsync(lead, previousStage);
		return ToDto(exam, lead);
	}

	private async Task RunJourneyAutomationAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmJourneyTrigger trigger)
	{
		await _leadAppService.RunJourneyRulesFromAutomationAsync(
			lead.AcademicYearId,
			new RunAdmissionCrmJourneyRulesDto
			{
				Trigger = trigger,
				LeadId = lead.Id,
				Stage = lead.Stage
			});
	}

	private async Task RunStageReachedJourneyIfChangedAsync(
		AdmissionInquiryLead lead,
		AdmissionInquiryStage previousStage)
	{
		if (lead.Stage == previousStage)
		{
			return;
		}

		await RunJourneyAutomationAsync(lead, AdmissionCrmJourneyTrigger.StageReached);
	}

	private async Task<AdmissionInquiryLead> GetLeadForCurrentTenantAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound")
				.WithData("LeadId", id);
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation);
		return lead;
	}

	private async Task<AdmissionCrmEntranceExamRecord> GetExamForCurrentTenantAsync(Guid id, string operation)
	{
		var exam = await _examRepository.GetAsync(id);
		if (exam.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:ExamRecordNotFound")
				.WithData("ExamRecordId", id);
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(exam.AcademicYearId, operation);
		return exam;
	}

	private async Task<AdmissionCrmEntranceExamRecord?> GetExamByLeadOrDefaultAsync(Guid leadId)
	{
		var queryable = await _examRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId));
	}

	private async Task<AdmissionCrmCycleConfiguration?> GetCycleConfigurationOrDefaultAsync(Guid academicYearId)
	{
		var queryable = await _cycleRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
	}

	private async Task EnsureSlotCapacityAsync(
		AdmissionCrmExamSlot slot,
		bool overrideCapacity,
		Guid? excludingExamRecordId)
	{
		var studentDetailsQueryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		var legacyCount = await AsyncExecuter.CountAsync(studentDetailsQueryable.Where(x =>
			x.EntranceExamSlotId == slot.Id));

		var examQueryable = await _examRepository.GetQueryableAsync();
		var crmCount = await AsyncExecuter.CountAsync(examQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.EntranceExamSlotId == slot.Id &&
			(!excludingExamRecordId.HasValue || x.Id != excludingExamRecordId.Value)));

		if (legacyCount + crmCount >= slot.StudentLimit && !overrideCapacity)
		{
			throw new BusinessException("AdmissionCrm:ExamSlotCapacityFull")
				.WithData("EntranceExamSlotId", slot.Id)
				.WithData("StudentLimit", slot.StudentLimit)
				.WithData("ScheduledCount", legacyCount + crmCount);
		}
	}

	private async Task EnsureOverrideCapacityPermissionAsync()
	{
		if (!await AuthorizationService.IsGrantedAsync(
			    ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamCapacity))
		{
			throw new AbpAuthorizationException("Capacity override permission is required.");
		}
	}

	private async Task EnsureEligibilityOverridePermissionAsync()
	{
		if (!await AuthorizationService.IsGrantedAsync(
			    ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility))
		{
			throw new AbpAuthorizationException("Eligibility override permission is required.");
		}
	}

	private async Task RecalculateClassWiseMeritRanksAsync(
		AdmissionInquiryLead targetLead,
		AdmissionCrmEntranceExamRecord currentExam,
		AdmissionCrmMeritRankingMode meritRankingMode)
	{
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var candidateLeads = await AsyncExecuter.ToListAsync(leadQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == targetLead.AcademicYearId &&
			x.MergedIntoLeadId == null));

		var sameClassLeads = candidateLeads
			.Where(x => IsSameMeritClass(targetLead, x))
			.ToList();
		var leadIds = sameClassLeads.Select(x => x.Id).ToList();
		if (leadIds.Count == 0)
		{
			return;
		}

		var examQueryable = await _examRepository.GetQueryableAsync();
		var exams = await AsyncExecuter.ToListAsync(examQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == targetLead.AcademicYearId &&
			leadIds.Contains(x.LeadId) &&
			x.ResultRecordedAt.HasValue &&
			x.ObtainedMarks.HasValue));

		if (exams.All(x => x.Id != currentExam.Id))
		{
			exams.Add(currentExam);
		}

		var leadById = sameClassLeads.ToDictionary(x => x.Id);
		var rankedCandidates = exams
			.Where(x => x.ObtainedMarks.HasValue && leadById.ContainsKey(x.LeadId))
			.Select(x => new { Exam = x, Lead = leadById[x.LeadId] })
			.ToList();

		var ordered = meritRankingMode switch
		{
			AdmissionCrmMeritRankingMode.MarksThenLeadScore => rankedCandidates
				.OrderByDescending(x => x.Exam.ObtainedMarks)
				.ThenByDescending(x => x.Lead.LeadScore)
				.ThenBy(x => x.Lead.CreationTime)
				.ThenBy(x => x.Lead.Id),
			AdmissionCrmMeritRankingMode.LeadScoreThenMarks => rankedCandidates
				.OrderByDescending(x => x.Lead.LeadScore)
				.ThenByDescending(x => x.Exam.ObtainedMarks)
				.ThenBy(x => x.Lead.CreationTime)
				.ThenBy(x => x.Lead.Id),
			_ => rankedCandidates
				.OrderByDescending(x => x.Exam.ObtainedMarks)
				.ThenBy(x => x.Lead.CreationTime)
				.ThenBy(x => x.Lead.Id)
		};

		var rank = 1;
		var changedExistingExams = new List<AdmissionCrmEntranceExamRecord>();
		foreach (var candidate in ordered)
		{
			candidate.Exam.SetMeritRank(rank++);
			if (candidate.Exam.Id != currentExam.Id)
			{
				changedExistingExams.Add(candidate.Exam);
			}
		}

		if (changedExistingExams.Count > 0)
		{
			await _examRepository.UpdateManyAsync(changedExistingExams, autoSave: false);
		}
	}

	private static bool IsSameMeritClass(AdmissionInquiryLead targetLead, AdmissionInquiryLead candidateLead)
	{
		if (targetLead.TargetStandardId.HasValue)
		{
			return candidateLead.TargetStandardId == targetLead.TargetStandardId;
		}

		var targetClass = NormalizeMeritClassName(targetLead.DesiredClassName);
		if (!string.IsNullOrWhiteSpace(targetClass))
		{
			return NormalizeMeritClassName(candidateLead.DesiredClassName) == targetClass;
		}

		return candidateLead.Id == targetLead.Id;
	}

	private static string? NormalizeMeritClassName(string? className)
	{
		return string.IsNullOrWhiteSpace(className)
			? null
			: className.Trim().ToUpperInvariant();
	}

	private static void EnsureExamReady(
		AdmissionInquiryLead lead,
		AdmissionCrmExamReadinessField examReadinessFields)
	{
		if (lead.Stage == AdmissionInquiryStage.Archived)
		{
			throw new BusinessException("AdmissionCrm:ArchivedLeadCannotScheduleExam")
				.WithData("LeadId", lead.Id);
		}

		var missingFields = new List<string>();
		if (examReadinessFields.HasFlag(AdmissionCrmExamReadinessField.Contact) &&
		    string.IsNullOrWhiteSpace(lead.PrimaryContactNo) &&
		    string.IsNullOrWhiteSpace(lead.Email))
		{
			missingFields.Add("Contact");
		}

		if (examReadinessFields.HasFlag(AdmissionCrmExamReadinessField.AdmissionIntent) &&
		    string.IsNullOrWhiteSpace(lead.AdmissionIntent))
		{
			missingFields.Add("Admission intent");
		}

		if (examReadinessFields.HasFlag(AdmissionCrmExamReadinessField.DesiredClass) &&
		    !lead.TargetStandardId.HasValue &&
		    string.IsNullOrWhiteSpace(lead.DesiredClassName))
		{
			missingFields.Add("Desired class");
		}

		if (examReadinessFields.HasFlag(AdmissionCrmExamReadinessField.StudentName) &&
		    string.IsNullOrWhiteSpace(lead.FirstName) &&
		    string.IsNullOrWhiteSpace(lead.MiddleName) &&
		    string.IsNullOrWhiteSpace(lead.LastName))
		{
			missingFields.Add("Student name");
		}

		if (examReadinessFields.HasFlag(AdmissionCrmExamReadinessField.ParentGuardianName) &&
		    string.IsNullOrWhiteSpace(lead.ParentGuardianName))
		{
			missingFields.Add("Parent or guardian name");
		}

		if (examReadinessFields.HasFlag(AdmissionCrmExamReadinessField.InquirySource) &&
		    !lead.InquirySourceId.HasValue &&
		    string.IsNullOrWhiteSpace(lead.InquirySourceSnapshot))
		{
			missingFields.Add("Inquiry source");
		}

		if (missingFields.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:ExamReadinessIncomplete")
				.WithData("LeadId", lead.Id)
				.WithData("MissingFields", string.Join(", ", missingFields));
		}
	}

	private async Task EnsureRegistrationFormReadyAsync(
		AdmissionInquiryLead lead,
		bool registrationFormRequiredBeforeExam)
	{
		if (!registrationFormRequiredBeforeExam)
		{
			return;
		}

		if (lead.RegistrationFormIssuedAt.HasValue &&
		    !string.IsNullOrWhiteSpace(lead.RegistrationFormNumber))
		{
			return;
		}

		if (lead.StudentId.HasValue)
		{
			var additionalDetail = await _studentAdditionalDetailRepository.FindAsync(lead.StudentId.Value);
			if (additionalDetail != null &&
			    additionalDetail.TenantId == CurrentTenant.Id &&
			    !string.IsNullOrWhiteSpace(additionalDetail.RegistrationFormNo))
			{
				return;
			}
		}

		throw new BusinessException("AdmissionCrm:RegistrationFormRequiredBeforeExam")
			.WithData("LeadId", lead.Id)
			.WithData("AcademicYearId", lead.AcademicYearId);
	}

	private static void ApplyLeadStageFromExam(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord exam)
	{
		var stage = exam.EligibilityDecision switch
		{
			AdmissionCrmEligibilityDecision.Eligible => AdmissionInquiryStage.Eligible,
			AdmissionCrmEligibilityDecision.Waitlisted => AdmissionInquiryStage.Waitlisted,
			AdmissionCrmEligibilityDecision.Rejected => AdmissionInquiryStage.Rejected,
			_ => exam.ResultRecordedAt.HasValue
				? AdmissionInquiryStage.ExamCompleted
				: AdmissionInquiryStage.ExamScheduled
		};

		if (lead.Stage != stage)
		{
			lead.ChangeStage(stage);
		}
	}

	private static AdmissionCrmEntranceExamDto ToDto(
		AdmissionCrmEntranceExamRecord exam,
		AdmissionInquiryLead? lead)
	{
		return new AdmissionCrmEntranceExamDto
		{
			Id = exam.Id,
			AcademicYearId = exam.AcademicYearId,
			LeadId = exam.LeadId,
			ApplicantName = lead == null ? string.Empty : BuildApplicantName(lead),
			EntranceExamSlotId = exam.EntranceExamSlotId,
			SlotNameSnapshot = exam.SlotNameSnapshot,
			ExamStartTime = exam.ExamStartTime,
			ExamEndTime = exam.ExamEndTime,
			ExamRoomNo = exam.ExamRoomNo,
			CapacityOverrideUsed = exam.CapacityOverrideUsed,
			CapacityOverrideReason = exam.CapacityOverrideReason,
			ScheduledAt = exam.ScheduledAt,
			AttendanceStatus = exam.AttendanceStatus,
			AttendanceMarkedAt = exam.AttendanceMarkedAt,
			TotalMarks = exam.TotalMarks,
			PassingMarks = exam.PassingMarks,
			ObtainedMarks = exam.ObtainedMarks,
			Percentage = exam.Percentage,
			ExamStatus = exam.ExamStatus,
			ResultRecordedAt = exam.ResultRecordedAt,
			MeritRank = exam.MeritRank,
			EligibilityDecision = exam.EligibilityDecision,
			EligibilityDecisionReason = exam.EligibilityDecisionReason,
			EligibilityOverrideUsed = exam.EligibilityOverrideUsed,
			EligibilityOverrideReason = exam.EligibilityOverrideReason,
			EligibilityDecidedAt = exam.EligibilityDecidedAt
		};
	}

	private static AdmissionCrmEligibilityHandoffDto ToHandoffDto(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord exam)
	{
		var missingAdmissionFields = GetMissingAdmissionHandoffFields(lead, exam);
		var canStartAdmission = exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible &&
		                        missingAdmissionFields.Count == 0;

		return new AdmissionCrmEligibilityHandoffDto
		{
			AcademicYearId = lead.AcademicYearId,
			LeadId = lead.Id,
			ExamRecordId = exam.Id,
			CanStartAdmission = canStartAdmission,
			RequiresManualReview = exam.EligibilityOverrideUsed ||
			                       !canStartAdmission,
			MissingAdmissionFields = missingAdmissionFields,
			ApplicantName = BuildApplicantName(lead),
			DateOfBirth = lead.DateOfBirth,
			ParentGuardianName = lead.ParentGuardianName,
			PrimaryContactNo = lead.PrimaryContactNo,
			Email = lead.Email,
			AdmissionIntent = lead.AdmissionIntent,
			TargetStandardId = lead.TargetStandardId,
			DesiredClassName = lead.DesiredClassName,
			InquirySourceId = lead.InquirySourceId,
			InquirySourceSnapshot = lead.InquirySourceSnapshot,
			LeadStage = lead.Stage,
			AttendanceStatus = exam.AttendanceStatus,
			ExamStatus = exam.ExamStatus,
			TotalMarks = exam.TotalMarks,
			PassingMarks = exam.PassingMarks,
			ObtainedMarks = exam.ObtainedMarks,
			Percentage = exam.Percentage,
			MeritRank = exam.MeritRank,
			EligibilityDecision = exam.EligibilityDecision,
			EligibilityDecisionReason = exam.EligibilityDecisionReason,
			EligibilityOverrideUsed = exam.EligibilityOverrideUsed,
			EligibilityOverrideReason = exam.EligibilityOverrideReason,
			ResultRecordedAt = exam.ResultRecordedAt,
			EligibilityDecidedAt = exam.EligibilityDecidedAt
		};
	}

	private static List<string> GetMissingAdmissionHandoffFields(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord exam)
	{
		var missingFields = new List<string>();
		if (string.IsNullOrWhiteSpace(lead.FirstName) &&
		    string.IsNullOrWhiteSpace(lead.MiddleName) &&
		    string.IsNullOrWhiteSpace(lead.LastName))
		{
			missingFields.Add("Applicant name");
		}

		if (string.IsNullOrWhiteSpace(lead.ParentGuardianName))
		{
			missingFields.Add("Parent or guardian name");
		}

		if (string.IsNullOrWhiteSpace(lead.PrimaryContactNo) &&
		    string.IsNullOrWhiteSpace(lead.Email))
		{
			missingFields.Add("Contact");
		}

		if (string.IsNullOrWhiteSpace(lead.AdmissionIntent))
		{
			missingFields.Add("Admission intent");
		}

		if (!lead.TargetStandardId.HasValue &&
		    string.IsNullOrWhiteSpace(lead.DesiredClassName))
		{
			missingFields.Add("Desired class");
		}

		if (!exam.ResultRecordedAt.HasValue &&
		    exam.ExamStatus is not EntranceExamStatusEnum.Absent)
		{
			missingFields.Add("Entrance exam result");
		}

		if (exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Pending)
		{
			missingFields.Add("Eligibility decision");
		}

		return missingFields;
	}

	private static string BuildApplicantName(AdmissionInquiryLead lead)
	{
		var name = string.Join(
				" ",
				new[] { lead.FirstName, lead.MiddleName, lead.LastName }
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Select(x => x!.Trim()))
			.Trim();

		return string.IsNullOrWhiteSpace(name)
			? lead.ParentGuardianName ?? lead.PrimaryContactNo ?? lead.Email ?? "Unnamed applicant"
			: name;
	}

	private async Task AddTimelineEventAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details,
		Guid? relatedEntityId)
	{
		await _timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				lead.Id,
				lead.AcademicYearId,
				eventType,
				title,
				details,
				relatedEntityId,
				CurrentUser.Id,
				new DateTimeOffset(Clock.Now)),
			autoSave: false);
	}
}
