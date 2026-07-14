using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmActivationAppService : ApplicationService, IAdmissionCrmActivationAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmNumberSeries, Guid> _seriesRepository;
	private readonly IRepository<AdmissionCrmIdentifierAllocation, Guid> _identifierRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmServiceHandoff, Guid> _serviceHandoffRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmAdmissionDecision, Guid> _decisionRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmSeatReservation, Guid> _reservationRepository;
	private readonly IRepository<AdmissionCrmSeatPool, Guid> _seatPoolRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmApplicantIdentityProfile, Guid> _identityRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _documentRepository;
	private readonly IRepository<AdmissionCrmPolicyAdoption, Guid> _policyAdoptionRepository;
	private readonly IRepository<AdmissionCrmPolicyPack, Guid> _policyPackRepository;
	private readonly IRepository<AdmissionCrmPolicyEvaluation, Guid> _policyEvaluationRepository;
	private readonly IRepository<AdmissionCrmRouteAssignment, Guid> _routeAssignmentRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<AttendanceEntry, Guid> _attendanceRepository;
	private readonly IRepository<DueFee, Guid> _dueFeeRepository;
	private readonly IStudentRepository _studentRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly AdmissionCrmStudentActivationOrchestrator _studentActivationOrchestrator;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmActivationAppService(
		IRepository<AdmissionCrmNumberSeries, Guid> seriesRepository,
		IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmServiceHandoff, Guid> serviceHandoffRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
		IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> documentRepository,
		IRepository<AdmissionCrmPolicyAdoption, Guid> policyAdoptionRepository,
		IRepository<AdmissionCrmPolicyPack, Guid> policyPackRepository,
		IRepository<AdmissionCrmPolicyEvaluation, Guid> policyEvaluationRepository,
		IRepository<AdmissionCrmRouteAssignment, Guid> routeAssignmentRepository,
		IRepository<EducationBoard, Guid> boardRepository,
		IRepository<EducationMedium, Guid> mediumRepository,
		IRepository<EducationMediumStage, Guid> stageRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Division, Guid> divisionRepository,
		IRepository<AttendanceEntry, Guid> attendanceRepository,
		IRepository<DueFee, Guid> dueFeeRepository,
		IStudentRepository studentRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		AdmissionCrmStudentActivationOrchestrator studentActivationOrchestrator,
		StudentScopeAccessService studentScopeAccessService)
	{
		_seriesRepository = seriesRepository; _identifierRepository = identifierRepository;
		_activationRepository = activationRepository; _serviceHandoffRepository = serviceHandoffRepository;
		_leadRepository = leadRepository;
		_decisionRepository = decisionRepository; _offerRepository = offerRepository;
		_reservationRepository = reservationRepository; _seatPoolRepository = seatPoolRepository;
		_cycleRepository = cycleRepository;
		_quotationRepository = quotationRepository; _identityRepository = identityRepository;
		_documentRepository = documentRepository; _policyAdoptionRepository = policyAdoptionRepository;
		_policyPackRepository = policyPackRepository; _policyEvaluationRepository = policyEvaluationRepository;
		_routeAssignmentRepository = routeAssignmentRepository;
		_boardRepository = boardRepository; _mediumRepository = mediumRepository; _stageRepository = stageRepository;
		_standardRepository = standardRepository; _divisionRepository = divisionRepository;
		_attendanceRepository = attendanceRepository; _dueFeeRepository = dueFeeRepository;
		_studentRepository = studentRepository; _timelineRepository = timelineRepository;
		_studentActivationOrchestrator = studentActivationOrchestrator; _studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageNumberSeries)]
	public async Task<AdmissionCrmNumberSeriesDto> CreateNumberSeriesAsync(Guid academicYearId, CreateAdmissionCrmNumberSeriesDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateNumberSeriesAsync));
		var queryable = await _seriesRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Code == input.Code.ToUpper())))
			throw new BusinessException("AdmissionCrm:NumberSeriesCodeAlreadyExists");
		var entity = new AdmissionCrmNumberSeries(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.Name, input.IdentifierType, input.EducationBoardId, input.AcademicMediumId,
			input.StandardId, input.Prefix, input.Suffix, input.Padding, input.StartingSequence,
			input.MaximumSequence, input.EffectiveFrom, input.EffectiveUntil);
		await _seriesRepository.InsertAsync(entity, autoSave: true);
		return MapSeries(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public async Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetNumberSeriesAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetNumberSeriesAsync));
		var queryable = await _seriesRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return entities.OrderBy(x => x.IdentifierType).ThenBy(x => x.Code).Select(MapSeries).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public async Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetPlacementOptionsAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetPlacementOptionsAsync));
		var boardQuery = await _boardRepository.GetQueryableAsync();
		var boards = await AsyncExecuter.ToListAsync(boardQuery
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)
			.Select(x => new { x.Id, x.Type }));
		var mediumQuery = await _mediumRepository.GetQueryableAsync();
		var mediums = await AsyncExecuter.ToListAsync(mediumQuery
			.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.EducationBoardId, x.Name }));
		var stageQuery = await _stageRepository.GetQueryableAsync();
		var stages = await AsyncExecuter.ToListAsync(stageQuery
			.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.EducationMediumId, x.Name }));
		var standardQuery = await _standardRepository.GetQueryableAsync();
		var standards = await AsyncExecuter.ToListAsync(standardQuery
			.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.EducationMediumStageId, x.Name }));
		var divisionQuery = await _divisionRepository.GetQueryableAsync();
		var divisions = await AsyncExecuter.ToListAsync(divisionQuery
			.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.StandardId, x.Name, x.Strength }));
		return (from board in boards
			join medium in mediums on board.Id equals medium.EducationBoardId
			join stage in stages on medium.Id equals stage.EducationMediumId
			join standard in standards on stage.Id equals standard.EducationMediumStageId
			join division in divisions on standard.Id equals division.StandardId
			orderby board.Type, medium.Name, stage.Name, standard.Name, division.Name
			select new AdmissionCrmPlacementOptionDto
			{
				EducationBoardId = board.Id, BoardName = board.Type.ToString(), AcademicMediumId = medium.Id,
				MediumName = medium.Name, AcademicMediumStageId = stage.Id, StageName = stage.Name,
				StandardId = standard.Id, StandardName = standard.Name, DivisionId = division.Id,
				DivisionName = division.Name, DivisionStrength = division.Strength
			}).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageNumberSeries)]
	public async Task<AdmissionCrmNumberSeriesDto> SubmitNumberSeriesAsync(Guid id)
	{
		var entity = await GetSeriesAsync(id);
		entity.Submit(GetCurrentUserId());
		await _seriesRepository.UpdateAsync(entity, autoSave: true);
		return MapSeries(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishNumberSeries)]
	public async Task<AdmissionCrmNumberSeriesDto> PublishNumberSeriesAsync(Guid id)
	{
		var entity = await GetSeriesAsync(id);
		var queryable = await _seriesRepository.GetQueryableAsync();
		var overlapping = await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == entity.AcademicYearId && x.Id != entity.Id &&
			x.IdentifierType == entity.IdentifierType && x.EducationBoardId == entity.EducationBoardId &&
			x.AcademicMediumId == entity.AcademicMediumId && x.StandardId == entity.StandardId &&
			x.Status == AdmissionCrmNumberSeriesStatus.Published &&
			(!x.EffectiveUntil.HasValue || x.EffectiveUntil >= entity.EffectiveFrom) &&
			(!entity.EffectiveUntil.HasValue || x.EffectiveFrom <= entity.EffectiveUntil)));
		if (overlapping) throw new BusinessException("AdmissionCrm:OverlappingPublishedNumberSeries");
		entity.Publish(GetCurrentUserId());
		await _seriesRepository.UpdateAsync(entity, autoSave: true);
		return MapSeries(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignManualIdentifier)]
	public async Task<AdmissionCrmIdentifierAllocationDto> AssignManualIdentifierAsync(
		Guid activationId, AssignManualAdmissionCrmIdentifierDto input)
	{
		var activation = await GetActivationAsync(activationId, nameof(AssignManualIdentifierAsync));
		if (activation.StudentId.HasValue || activation.Status is AdmissionCrmActivationStatus.InProgress or
			AdmissionCrmActivationStatus.FirstDayReady or AdmissionCrmActivationStatus.Cancelled or
			AdmissionCrmActivationStatus.CancellationPending)
			throw new BusinessException("AdmissionCrm:ManualIdentifierAssignmentClosed");
		if (input.Sequence <= 0 || input.Sequence > int.MaxValue)
			throw new BusinessException("AdmissionCrm:InvalidManualIdentifierSequence");
		var series = await GetSeriesAsync(input.NumberSeriesId);
		if (series.Id != activation.GrNumberSeriesId || series.IdentifierType != AdmissionCrmIdentifierType.GrNumber ||
			series.Status != AdmissionCrmNumberSeriesStatus.Published)
			throw new BusinessException("AdmissionCrm:ActivationNumberSeriesMismatch");
		var queryable = await _identifierRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			(x.ActivationId == activation.Id && x.IdentifierType == AdmissionCrmIdentifierType.GrNumber ||
			 x.IdentifierType == AdmissionCrmIdentifierType.GrNumber && x.Value == input.Value ||
			 x.NumberSeriesId == series.Id && x.Sequence == input.Sequence))))
			throw new BusinessException("AdmissionCrm:IdentifierAlreadyAllocated");
		var allocation = new AdmissionCrmIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id,
			activation.AcademicYearId, activation.LeadId, activation.Id, series.Id,
			AdmissionCrmIdentifierType.GrNumber, input.Sequence, input.Value, Clock.Now);
		allocation.MarkManual(GetCurrentUserId(), input.Reason);
		await _identifierRepository.InsertAsync(allocation, autoSave: true);
		var lead = await GetLeadAsync(activation.LeadId, nameof(AssignManualIdentifierAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.IdentifierAllocated,
			"GR number manually assigned", $"{input.Value}. Reason: {input.Reason}", allocation.Id);
		return MapIdentifier(allocation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public async Task<AdmissionCrmIdentifierGovernanceReportDto> GetIdentifierGovernanceReportAsync(
		Guid academicYearId, Guid numberSeriesId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetIdentifierGovernanceReportAsync));
		var series = await GetSeriesAsync(numberSeriesId);
		if (series.AcademicYearId != academicYearId)
			throw new BusinessException("AdmissionCrm:NumberSeriesAcademicYearMismatch");
		var queryable = await _identifierRepository.GetQueryableAsync();
		var allocations = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.NumberSeriesId == numberSeriesId));
		var used = allocations.Select(x => x.Sequence).ToHashSet();
		var gapCount = Math.Max(0, series.NextSequence - series.StartingSequence - used.Count(x =>
			x >= series.StartingSequence && x < series.NextSequence));
		var gaps = Enumerable.Range(0, (int)Math.Min(series.NextSequence - series.StartingSequence, 100_000))
			.Select(offset => series.StartingSequence + offset).Where(sequence => !used.Contains(sequence)).Take(500).ToList();
		return new AdmissionCrmIdentifierGovernanceReportDto
		{
			AcademicYearId = academicYearId, NumberSeriesId = series.Id, SeriesCode = series.Code,
			StartingSequence = series.StartingSequence, NextSequence = series.NextSequence,
			MaximumSequence = series.MaximumSequence, AllocatedCount = allocations.Count,
			CommittedCount = allocations.Count(x => x.Status == AdmissionCrmIdentifierStatus.Committed),
			ManualCount = allocations.Count(x => x.IsManual),
			VoidedCount = allocations.Count(x => x.Status == AdmissionCrmIdentifierStatus.Void),
			IsExhausted = series.MaximumSequence.HasValue && series.NextSequence > series.MaximumSequence.Value,
			GapSequences = gaps, GapReportTruncated = gapCount > gaps.Count,
			Allocations = allocations.OrderBy(x => x.Sequence).Select(MapIdentifier).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PreviewActivation)]
	public async Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmActivationDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(PreviewAsync));
		return await BuildPreviewAsync(lead, input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RequestActivation)]
	public async Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, RequestAdmissionCrmActivationDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(RequestAsync));
		var existing = await FindActivationAsync(leadId);
		if (existing != null) return await MapActivationAsync(existing);
		var preview = await BuildPreviewAsync(lead, input);
		if (!preview.IsReady) throw new BusinessException("AdmissionCrm:ActivationReadinessFailed")
			.WithData("BlockingItems", string.Join(",", preview.Items.Where(x => x.Blocking && x.Status == AdmissionCrmActivationStepStatus.Failed).Select(x => x.Key)));
		var reference = $"ACT-{Clock.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();
		var entity = new AdmissionCrmActivation(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, reference, input.EducationBoardId, input.AcademicMediumId, input.AcademicMediumStageId,
			input.StandardId, input.DivisionId, input.GrNumberSeriesId, input.RollNumber, input.Gender,
			Serialize(preview), Serialize(input.ServiceHandoffs), input.RequireStudentAccount, input.RequireGuardianAccount,
			GetCurrentUserId(), Clock.Now);
		await _activationRepository.InsertAsync(entity, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ActivationRequested,
			"Student activation requested", reference, entity.Id);
		return await MapActivationAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveActivation)]
	public async Task<AdmissionCrmActivationDto> ApproveAsync(Guid id)
	{
		var entity = await GetActivationAsync(id, nameof(ApproveAsync));
		entity.Approve(GetCurrentUserId(), Clock.Now);
		await _activationRepository.UpdateAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(entity.LeadId, nameof(ApproveAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ActivationApproved,
			"Student activation approved", entity.ActivationReference, entity.Id);
		return await MapActivationAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation)]
	public async Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id)
	{
		var activation = await GetActivationAsync(id, nameof(ExecuteAsync));
		if (activation.Status == AdmissionCrmActivationStatus.FirstDayReady) return await MapActivationAsync(activation);
		activation.StartAttempt();
		var steps = Deserialize<List<AdmissionCrmActivationStepDto>>(activation.StepsJson) ?? [];
		var lead = await GetLeadAsync(activation.LeadId, nameof(ExecuteAsync));
		try
		{
			var student = lead.StudentId.HasValue ? await _studentRepository.GetAsync(lead.StudentId.Value) : null;
			AdmissionCrmIdentifierAllocation? identifier = null;
			if (student == null)
			{
				identifier = await GetOrAllocateIdentifierAsync(activation, lead, steps);
				if (identifier.Sequence > int.MaxValue) throw new BusinessException("AdmissionCrm:GrSequenceExceedsStudentLimit");
				if (!lead.DateOfBirth.HasValue) throw new BusinessException("AdmissionCrm:ActivationDateOfBirthRequired");
				student = Student.CreateDirectRegistration(GuidGenerator.Create(), CurrentTenant.Id,
					lead.FirstName, lead.MiddleName, lead.LastName, lead.AcademicYearId, activation.EducationBoardId,
					activation.AcademicMediumId, activation.AcademicMediumStageId, activation.StandardId,
					activation.DivisionId, lead.DateOfBirth.Value, activation.Gender,
					lead.PrimaryContactNo ?? string.Empty, email: lead.Email);
				student.MoveToAdmission();
				student.AssignGrNo((int)identifier.Sequence);
				if (activation.RollNumber.HasValue) student.AssignRollNo(activation.RollNumber.Value);
				await _studentRepository.InsertAsync(student, autoSave: true);
				lead.LinkStudent(student.Id);
				await _leadRepository.UpdateAsync(lead, autoSave: true);
				activation.LinkStudent(student.Id);
				identifier.Commit(student.Id, Clock.Now);
				await _identifierRepository.UpdateAsync(identifier, autoSave: true);
				UpsertStep(steps, "student_conversion", AdmissionCrmActivationStepStatus.Completed, $"Student {student.Id} created.");
			}
			else
			{
				activation.LinkStudent(student.Id);
				UpsertStep(steps, "student_conversion", AdmissionCrmActivationStepStatus.Completed, $"Existing student {student.Id} reused.");
			}

			if (student.Status != StudentLifeCycleStatusEnum.Enrolled)
			{
				var outcome = await _studentActivationOrchestrator.ActivateAsync(
					student.Id,
					activation.RequireStudentAccount,
					activation.RequireGuardianAccount,
					$"Activated from {activation.ActivationReference}.");
				if (!outcome.Success)
				{
					var failure = outcome.Message;
					UpsertStep(steps, "accounts_and_enrollment", AdmissionCrmActivationStepStatus.Failed, failure);
					activation.UpdateSteps(Serialize(steps)); activation.RequireRepair("accounts_and_enrollment", failure);
					await _activationRepository.UpdateAsync(activation, autoSave: true);
					await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ActivationRepairRequired, "Activation repair required", failure, activation.Id);
					return await MapActivationAsync(activation);
				}
			}
			UpsertStep(steps, "academic_placement", AdmissionCrmActivationStepStatus.Completed, "Academic placement committed.");
			UpsertStep(steps, "accounts_and_enrollment", AdmissionCrmActivationStepStatus.Completed, "Enrollment and required access completed.");
			await EnsureServiceHandoffsAsync(activation, lead, student.Id, steps);
			await MarkAcceptedSeatActivatedAsync(lead.Id);
			activation.UpdateSteps(Serialize(steps)); activation.Complete(Clock.Now);
			await _activationRepository.UpdateAsync(activation, autoSave: true);
			await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.StudentActivated, "Student is first-day ready", activation.ActivationReference, activation.Id);
			return await MapActivationAsync(activation);
		}
		catch (Exception exception) when (exception is not AbpAuthorizationException)
		{
			var message = exception is BusinessException business ? business.Code ?? business.Message : "Unexpected activation failure.";
			UpsertStep(steps, "execution", AdmissionCrmActivationStepStatus.Failed, message);
			activation.UpdateSteps(Serialize(steps)); activation.RequireRepair("execution", message);
			await _activationRepository.UpdateAsync(activation, autoSave: true);
			await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ActivationRepairRequired, "Activation repair required", message, activation.Id);
			return await MapActivationAsync(activation);
		}
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission)]
	public async Task<AdmissionCrmActivationCancellationPreviewDto> PreviewCancellationAsync(Guid id)
	{
		var activation = await GetActivationAsync(id, nameof(PreviewCancellationAsync));
		return await BuildCancellationPreviewAsync(activation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission)]
	public async Task<AdmissionCrmActivationDto> RequestCancellationAsync(
		Guid id,
		RequestAdmissionCrmActivationCancellationDto input)
	{
		var activation = await GetActivationAsync(id, nameof(RequestCancellationAsync));
		var preview = await BuildCancellationPreviewAsync(activation);
		if (!preview.CanCancel)
		{
			throw new BusinessException("AdmissionCrm:ActivationCancellationChecksFailed")
				.WithData("FailedChecks", string.Join(",", preview.Checks.Where(x => !x.Passed).Select(x => x.Key)));
		}

		activation.RequestCancellation(input.Reason, GetCurrentUserId(), Clock.Now);
		await _activationRepository.UpdateAsync(activation, autoSave: true);
		var lead = await GetLeadAsync(activation.LeadId, nameof(RequestCancellationAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ActivationCancellationRequested,
			"Activation cancellation requested", input.Reason, activation.Id);
		return await MapActivationAsync(activation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission)]
	public async Task<AdmissionCrmActivationDto> ApproveCancellationAsync(Guid id)
	{
		var activation = await GetActivationAsync(id, nameof(ApproveCancellationAsync));
		if (activation.Status != AdmissionCrmActivationStatus.CancellationPending)
			throw new BusinessException("AdmissionCrm:ActivationCancellationNotPending");
		if (activation.CancellationRequestedByUserId == GetCurrentUserId())
			throw new BusinessException("AdmissionCrm:DistinctActivationCancellationApproverRequired");

		var preview = await BuildCancellationPreviewAsync(activation);
		if (!preview.CanCancel)
		{
			throw new BusinessException("AdmissionCrm:ActivationCancellationChecksFailed")
				.WithData("FailedChecks", string.Join(",", preview.Checks.Where(x => !x.Passed).Select(x => x.Key)));
		}

		if (!activation.StudentId.HasValue)
			throw new BusinessException("AdmissionCrm:ActivationStudentMissing");
		var student = await _studentRepository.GetInquiryForUpdateAsync(activation.StudentId.Value)
			?? throw new BusinessException("AdmissionCrm:ActivationStudentMissing");
		student.ChangeOperationalStatus(StudentStatus.Inactive, activation.CancellationReason);
		await _studentRepository.UpdateAsync(student, autoSave: true);

		var identifierQuery = await _identifierRepository.GetQueryableAsync();
		var identifiers = await AsyncExecuter.ToListAsync(identifierQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activation.Id));
		foreach (var identifier in identifiers.Where(x => x.Status is AdmissionCrmIdentifierStatus.Allocated or AdmissionCrmIdentifierStatus.Committed))
		{
			identifier.Void(activation.CancellationReason!);
			await _identifierRepository.UpdateAsync(identifier);
		}

		var offerQuery = await _offerRepository.GetQueryableAsync();
		var offers = await AsyncExecuter.ToListAsync(offerQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == activation.LeadId));
		var offer = offers.Where(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted)
			.OrderByDescending(x => x.RespondedAt).FirstOrDefault();
		if (offer != null)
		{
			offer.CancelAccepted(activation.CancellationReason!, Clock.Now);
			await _offerRepository.UpdateAsync(offer);
			if (offer.ReservationId.HasValue)
			{
				var reservation = await _reservationRepository.FindAsync(offer.ReservationId.Value);
				if (reservation?.TenantId == CurrentTenant.Id)
				{
					reservation.Release(activation.CancellationReason!, Clock.Now, cancelled: true);
					await _reservationRepository.UpdateAsync(reservation);
				}
			}
			var pool = await _seatPoolRepository.GetAsync(offer.SeatPoolId);
			if (pool.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException("Seat pool belongs to another tenant.");
			pool.CancelCommittedSeat();
			await _seatPoolRepository.UpdateAsync(pool);
		}

		var cancelledDecision = new AdmissionCrmAdmissionDecision(GuidGenerator.Create(), CurrentTenant.Id,
			activation.AcademicYearId, activation.LeadId);
		cancelledDecision.Decide(AdmissionCrmAdmissionDecisionStatus.Cancelled, offer?.SeatPoolId, offer?.Id,
			activation.CancellationReason!, false, null, GetCurrentUserId(), Clock.Now);
		await _decisionRepository.InsertAsync(cancelledDecision);

		activation.Cancel(GetCurrentUserId(), Clock.Now);
		await _activationRepository.UpdateAsync(activation, autoSave: true);
		var lead = await GetLeadAsync(activation.LeadId, nameof(ApproveCancellationAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ActivationCancelled,
			"Student activation cancelled", activation.CancellationReason!, activation.Id);
		return await MapActivationAsync(activation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public async Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetByLeadAsync));
		var entity = await FindActivationAsync(leadId);
		return entity == null ? null : await MapActivationAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation)]
	public async Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetServiceHandoffsAsync(Guid activationId)
	{
		await GetActivationAsync(activationId, nameof(GetServiceHandoffsAsync));
		var queryable = await _serviceHandoffRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activationId));
		return entities.OrderBy(x => x.Type).Select(MapServiceHandoff).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageServiceHandoffs)]
	public async Task<AdmissionCrmServiceHandoffDto> UpdateServiceHandoffAsync(
		Guid id, UpdateAdmissionCrmServiceHandoffDto input)
	{
		var entity = await _serviceHandoffRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(UpdateServiceHandoffAsync));
		switch (input.Status)
		{
			case AdmissionCrmServiceHandoffStatus.Dispatched:
				entity.MarkDispatched(Clock.Now, input.ExternalReference);
				break;
			case AdmissionCrmServiceHandoffStatus.Acknowledged:
				entity.Acknowledge(Clock.Now, input.ExternalReference ?? throw new BusinessException("AdmissionCrm:ServiceHandoffReferenceRequired"));
				break;
			case AdmissionCrmServiceHandoffStatus.Failed:
				entity.Fail(input.Reason ?? throw new BusinessException("AdmissionCrm:ServiceHandoffFailureReasonRequired"));
				break;
			case AdmissionCrmServiceHandoffStatus.Cancelled:
				entity.Cancel(input.Reason ?? throw new BusinessException("AdmissionCrm:ServiceHandoffCancellationReasonRequired"));
				break;
			default:
				throw new BusinessException("AdmissionCrm:InvalidServiceHandoffTransition");
		}
		await _serviceHandoffRepository.UpdateAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(entity.LeadId, nameof(UpdateServiceHandoffAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ServiceHandoffChanged,
			$"{entity.Type} handoff {entity.Status}", input.Reason ?? input.ExternalReference ?? entity.Status.ToString(), entity.Id);
		return MapServiceHandoff(entity);
	}

	private async Task<AdmissionCrmActivationPreviewDto> BuildPreviewAsync(AdmissionInquiryLead lead, PreviewAdmissionCrmActivationDto input)
	{
		var items = new List<AdmissionCrmActivationReadinessItemDto>();
		void Item(string key, string label, bool passed, string detail, bool blocking = true) => items.Add(new()
		{
			Key = key, Label = label, Blocking = blocking, Detail = detail,
			Status = passed ? AdmissionCrmActivationStepStatus.Completed : AdmissionCrmActivationStepStatus.Failed
		});

		var decisionQuery = await _decisionRepository.GetQueryableAsync();
		var decisions = await AsyncExecuter.ToListAsync(decisionQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var decision = decisions.OrderByDescending(x => x.DecidedAt).FirstOrDefault();
		Item("admission_decision", "Final admission approval", decision?.Status == AdmissionCrmAdmissionDecisionStatus.Approved, decision?.Reason ?? "Final approval is missing.");

		var offerAccepted = true;
		if (decision?.OfferId.HasValue == true)
		{
			var offer = await _offerRepository.FindAsync(decision.OfferId.Value);
			offerAccepted = offer?.Status == AdmissionCrmSeatOfferStatus.Accepted;
		}
		Item("seat_offer", "Offer and seat commitment", offerAccepted, offerAccepted ? "Offer route is satisfied." : "The linked offer is not accepted.");

		var quoteQuery = await _quotationRepository.GetQueryableAsync();
		var quotes = await AsyncExecuter.ToListAsync(quoteQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.Status == AdmissionCrmQuotationStatus.Issued));
		var activationQuote = quotes.OrderByDescending(x => x.Version).FirstOrDefault(x => x.GateMilestone == AdmissionCrmPaymentGateMilestone.StudentActivation);
		var financialReady = activationQuote == null || activationQuote.IsGateSatisfiedAt(Clock.Now);
		Item("payment_gate", "Activation payment gate", financialReady, financialReady ? "Financial condition is satisfied or not required." : "Finance has not confirmed the activation gate.");

		var identityQuery = await _identityRepository.GetQueryableAsync();
		var identity = await AsyncExecuter.FirstOrDefaultAsync(identityQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var identityReady = identity?.Status is AdmissionCrmIdentityResolutionStatus.Ready or AdmissionCrmIdentityResolutionStatus.Overridden;
		Item("identity", "Household and identity", identityReady, identityReady ? "Identity resolution is complete." : "Household identity resolution is not ready.");
		if (input.RequireGuardianAccount) Item("guardian_access", "Guardian access", identity?.AccountLinkReady == true, identity?.AccountLinkReady == true ? "Guardian portal account is linked." : "A required guardian portal account is not linked.");

		var documentQuery = await _documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(documentQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var currentDocuments = documents.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
		var documentsReady = currentDocuments == null || currentDocuments.ReadinessConfirmedAt.HasValue;
		Item("documents", "Document readiness", documentsReady, documentsReady ? "Document checklist is ready or not applicable." : "Document readiness is not confirmed.");

		var policyReady = await IsPolicyReadyAsync(lead);
		Item("policy", "Policy evaluation", policyReady, policyReady ? "Applicable policy rules are satisfied." : "Blocking policy rules are missing or unresolved.");
		var routeQuery = await _routeAssignmentRepository.GetQueryableAsync();
		var route = await AsyncExecuter.FirstOrDefaultAsync(routeQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.IsPrimary));
		var routeReady = route == null || route.Status is AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved;
		Item("admission_route", "Admission route eligibility", routeReady, routeReady ? "Admission route is eligible or not route-specific." : "The assigned admission route is not eligible.");

		var placement = await ValidatePlacementAsync(lead, input);
		Item("placement", "Academic placement", placement.Valid, placement.Detail);
		var series = await _seriesRepository.FindAsync(input.GrNumberSeriesId);
		var seriesReady = series != null && series.TenantId == CurrentTenant.Id && series.AcademicYearId == lead.AcademicYearId &&
			series.IdentifierType == AdmissionCrmIdentifierType.GrNumber && series.Status == AdmissionCrmNumberSeriesStatus.Published;
		Item("numbering", "GR number series", seriesReady, seriesReady ? "Published GR series is available." : "A published GR series is required.");
		Item("student_identity", "Required applicant data", lead.DateOfBirth.HasValue && !string.IsNullOrWhiteSpace(lead.PrimaryContactNo),
			lead.DateOfBirth.HasValue && !string.IsNullOrWhiteSpace(lead.PrimaryContactNo) ? "Date of birth and contact are available." : "Date of birth and primary contact are required.");
		if (input.RequireStudentAccount) Item("student_access", "Student account input", !string.IsNullOrWhiteSpace(lead.Email), !string.IsNullOrWhiteSpace(lead.Email) ? "Student email is available." : "Student email is required for access provisioning.");
		foreach (var handoff in input.ServiceHandoffs)
		{
			var valid = !string.IsNullOrWhiteSpace(handoff.DestinationModule) && handoff.ConsentConfirmed;
			Item($"service_handoff_{handoff.Type}", $"{handoff.Type} service handoff", valid,
				valid ? $"A minimum-data request will be sent to {handoff.DestinationModule}." :
				"The optional service request needs a destination and confirmed consent before dispatch.", blocking: false);
		}

		return new AdmissionCrmActivationPreviewDto
		{
			LeadId = lead.Id, Items = items, IsReady = items.All(x => !x.Blocking || x.Status == AdmissionCrmActivationStepStatus.Completed),
			ExistingStudentId = lead.StudentId?.ToString(),
			GrNumberPreview = series == null ? string.Empty : $"{series.Prefix}{series.NextSequence.ToString().PadLeft(series.Padding, '0')}{series.Suffix}",
			DivisionStrength = placement.Strength, DivisionOccupancy = placement.Occupancy
		};
	}

	private async Task<AdmissionCrmActivationCancellationPreviewDto> BuildCancellationPreviewAsync(AdmissionCrmActivation activation)
	{
		var checks = new List<AdmissionCrmActivationCancellationCheckDto>();
		void Check(string key, bool passed, string detail) => checks.Add(new() { Key = key, Passed = passed, Detail = detail });

		var stateAllowed = activation.Status is AdmissionCrmActivationStatus.FirstDayReady or AdmissionCrmActivationStatus.CancellationPending;
		Check("activation_state", stateAllowed, stateAllowed
			? "Activation is eligible for cancellation review."
			: "Only a first-day-ready activation can enter cancellation review.");

		var cycleQuery = await _cycleRepository.GetQueryableAsync();
		var cycle = await AsyncExecuter.FirstOrDefaultAsync(cycleQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == activation.AcademicYearId));
		var windowDays = cycle?.ActivationCancellationWindowDays ?? 7;
		var windowEndsAt = activation.FirstDayReadyAt?.AddDays(windowDays);
		var windowOpen = windowDays > 0 && windowEndsAt.HasValue && Clock.Now <= windowEndsAt.Value;
		Check("safe_window", windowOpen, windowOpen
			? $"Cancellation window remains open until {windowEndsAt:O}."
			: "The configured same-cycle cancellation window is closed or disabled.");

		var studentExists = activation.StudentId.HasValue;
		Check("student_link", studentExists, studentExists ? "Activated Student is linked." : "Activated Student linkage is missing.");
		if (activation.StudentId.HasValue)
		{
			var attendanceQuery = await _attendanceRepository.GetQueryableAsync();
			var hasAttendance = await AsyncExecuter.AnyAsync(attendanceQuery.Where(x =>
				x.TenantId == CurrentTenant.Id && x.StudentId == activation.StudentId.Value));
			Check("attendance_activity", !hasAttendance, hasAttendance
				? "Attendance activity exists; use the Student Records leaving workflow."
				: "No attendance activity exists.");

			var feeQuery = await _dueFeeRepository.GetQueryableAsync();
			var hasFinancialActivity = await AsyncExecuter.AnyAsync(feeQuery.Where(x =>
				x.TenantId == CurrentTenant.Id && x.StudentId == activation.StudentId.Value &&
				(x.PaidAmount > 0 || x.RefundedAmount > 0)));
			Check("finance_activity", !hasFinancialActivity, hasFinancialActivity
				? "Posted payment or refund activity exists; Finance and Student Records must handle the case."
				: "No posted payment or refund activity exists.");
		}

		return new AdmissionCrmActivationCancellationPreviewDto
		{
			ActivationId = activation.Id,
			CancellationWindowEndsAt = windowEndsAt,
			Checks = checks,
			CanCancel = checks.All(x => x.Passed)
		};
	}

	private async Task<(bool Valid, string Detail, int Strength, int Occupancy)> ValidatePlacementAsync(
		AdmissionInquiryLead lead, PreviewAdmissionCrmActivationDto input)
	{
		var board = await _boardRepository.FindAsync(input.EducationBoardId);
		var medium = await _mediumRepository.FindAsync(input.AcademicMediumId);
		var stage = await _stageRepository.FindAsync(input.AcademicMediumStageId);
		var standard = await _standardRepository.FindAsync(input.StandardId);
		var division = await _divisionRepository.FindAsync(input.DivisionId);
		var relationsValid = board?.AcademicYearId == lead.AcademicYearId && medium?.EducationBoardId == board?.Id &&
			stage?.EducationMediumId == medium?.Id && standard?.EducationMediumStageId == stage?.Id && division?.StandardId == standard?.Id;
		if (!relationsValid) return (false, "Board, medium, stage, class, and division are not a valid active hierarchy.", division?.Strength ?? 0, 0);
		var students = await _studentRepository.GetByDivisionAsync(division!.Id, lead.AcademicYearId);
		var occupancy = students.Count(x => x.Status == StudentLifeCycleStatusEnum.Enrolled);
		if (occupancy >= division.Strength) return (false, $"Division capacity is full ({occupancy}/{division.Strength}).", division.Strength, occupancy);
		if (input.RollNumber.HasValue && students.Any(x => x.RollNo == input.RollNumber)) return (false, "Roll number is already assigned in this division.", division.Strength, occupancy);
		return (true, $"Placement is valid with {division.Strength - occupancy} seat(s) remaining.", division.Strength, occupancy);
	}

	private async Task<bool> IsPolicyReadyAsync(AdmissionInquiryLead lead)
	{
		var adoptionQuery = await _policyAdoptionRepository.GetQueryableAsync();
		var adoptions = await AsyncExecuter.ToListAsync(adoptionQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId && x.IsActive));
		if (adoptions.Count == 0) return true;
		var evaluationQuery = await _policyEvaluationRepository.GetQueryableAsync();
		var allEvaluations = await AsyncExecuter.ToListAsync(evaluationQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		foreach (var adoption in adoptions)
		{
			var pack = await _policyPackRepository.GetAsync(adoption.PolicyPackId);
			var rules = Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(pack.RulesJson) ?? [];
			foreach (var rule in rules.Where(x => x.Severity is AdmissionCrmPolicyRuleSeverity.Blocking or AdmissionCrmPolicyRuleSeverity.ApprovalRequired))
			{
				if (!string.IsNullOrWhiteSpace(rule.DesiredClass) && !rule.DesiredClass.Equals(lead.DesiredClassName, StringComparison.OrdinalIgnoreCase)) continue;
				var latest = allEvaluations.Where(x => x.PolicyAdoptionId == adoption.Id && x.RuleCode == rule.Code.ToUpperInvariant()).OrderByDescending(x => x.EvaluatedAt).FirstOrDefault();
				if (latest?.Status is not (AdmissionCrmPolicyEvaluationStatus.Satisfied or AdmissionCrmPolicyEvaluationStatus.ExceptionApproved or AdmissionCrmPolicyEvaluationStatus.NotApplicable)) return false;
			}
		}
		return true;
	}

	private async Task<AdmissionCrmIdentifierAllocation> GetOrAllocateIdentifierAsync(AdmissionCrmActivation activation,
		AdmissionInquiryLead lead, List<AdmissionCrmActivationStepDto> steps)
	{
		var queryable = await _identifierRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.ActivationId == activation.Id && x.IdentifierType == AdmissionCrmIdentifierType.GrNumber));
		if (existing != null) return existing;
		var series = await GetSeriesAsync(activation.GrNumberSeriesId);
		var value = series.Allocate(Clock.Now);
		await _seriesRepository.UpdateAsync(series, autoSave: true);
		var allocation = new AdmissionCrmIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id,
			lead.AcademicYearId, lead.Id, activation.Id, series.Id, AdmissionCrmIdentifierType.GrNumber,
			value.Sequence, value.Value, Clock.Now);
		await _identifierRepository.InsertAsync(allocation, autoSave: true);
		UpsertStep(steps, "gr_number", AdmissionCrmActivationStepStatus.Completed, $"Allocated {value.Value}.");
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.IdentifierAllocated, "GR number allocated", value.Value, allocation.Id);
		return allocation;
	}

	private async Task MarkAcceptedSeatActivatedAsync(Guid leadId)
	{
		var offerQuery = await _offerRepository.GetQueryableAsync();
		var offer = await AsyncExecuter.FirstOrDefaultAsync(offerQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.Status == AdmissionCrmSeatOfferStatus.Accepted));
		if (offer == null) return;
		var pool = await _seatPoolRepository.GetAsync(offer.SeatPoolId);
		if (pool.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException("Seat pool belongs to another tenant.");
		pool.MarkActivated();
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
	}

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation);
		return lead;
	}

	private async Task<AdmissionCrmNumberSeries> GetSeriesAsync(Guid id)
	{
		var entity = await _seriesRepository.GetAsync(id);
		if (entity.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException("Number series belongs to another tenant.");
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(GetSeriesAsync));
		return entity;
	}

	private async Task<AdmissionCrmActivation> GetActivationAsync(Guid id, string operation)
	{
		var entity = await _activationRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, operation);
		return entity;
	}

	private async Task<AdmissionCrmActivation?> FindActivationAsync(Guid leadId)
	{
		var queryable = await _activationRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	private async Task<AdmissionCrmActivationDto> MapActivationAsync(AdmissionCrmActivation x)
	{
		var queryable = await _identifierRepository.GetQueryableAsync();
		var allocations = await AsyncExecuter.ToListAsync(queryable.Where(y => y.TenantId == CurrentTenant.Id && y.ActivationId == x.Id));
		var handoffQueryable = await _serviceHandoffRepository.GetQueryableAsync();
		var handoffs = await AsyncExecuter.ToListAsync(handoffQueryable.Where(y =>
			y.TenantId == CurrentTenant.Id && y.ActivationId == x.Id));
		return new AdmissionCrmActivationDto
		{
			Id = x.Id, AcademicYearId = x.AcademicYearId, LeadId = x.LeadId, StudentId = x.StudentId,
			ActivationReference = x.ActivationReference, Status = x.Status, EducationBoardId = x.EducationBoardId,
			AcademicMediumId = x.AcademicMediumId, AcademicMediumStageId = x.AcademicMediumStageId,
			StandardId = x.StandardId, DivisionId = x.DivisionId, GrNumberSeriesId = x.GrNumberSeriesId,
			RollNumber = x.RollNumber, Gender = x.Gender,
			RequireStudentAccount = x.RequireStudentAccount, RequireGuardianAccount = x.RequireGuardianAccount,
			Steps = Deserialize<IReadOnlyList<AdmissionCrmActivationStepDto>>(x.StepsJson) ?? [],
			Identifiers = allocations.Select(MapIdentifier).ToList(), AttemptCount = x.AttemptCount,
			RepairStep = x.RepairStep, RepairReason = x.RepairReason, FirstDayReadyAt = x.FirstDayReadyAt
			, CancellationRequestedByUserId = x.CancellationRequestedByUserId,
			CancellationRequestedAt = x.CancellationRequestedAt, CancellationReason = x.CancellationReason,
			CancelledByUserId = x.CancelledByUserId, CancelledAt = x.CancelledAt,
			ServiceHandoffs = handoffs.Select(MapServiceHandoff).ToList()
		};
	}

	private async Task EnsureServiceHandoffsAsync(AdmissionCrmActivation activation, AdmissionInquiryLead lead,
		Guid studentId, List<AdmissionCrmActivationStepDto> steps)
	{
		var requests = Deserialize<IReadOnlyList<AdmissionCrmServiceHandoffRequestDto>>(activation.ServiceHandoffRequestsJson) ?? [];
		if (requests.Count == 0)
		{
			UpsertStep(steps, "service_handoffs", AdmissionCrmActivationStepStatus.NotRequired, "No optional service handoffs were requested.");
			return;
		}
		var queryable = await _serviceHandoffRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activation.Id));
		foreach (var request in requests.Where(request => existing.All(x => x.Type != request.Type)))
		{
			var entity = new AdmissionCrmServiceHandoff(GuidGenerator.Create(), CurrentTenant.Id,
				activation.AcademicYearId, lead.Id, activation.Id, studentId, request.Type,
				request.DestinationModule, request.ConsentConfirmed, Serialize(request.Payload), Clock.Now);
			await _serviceHandoffRepository.InsertAsync(entity, autoSave: true);
			await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.ServiceHandoffRequested,
				$"{request.Type} service handoff requested", request.DestinationModule, entity.Id);
		}
		UpsertStep(steps, "service_handoffs", AdmissionCrmActivationStepStatus.Completed,
			$"{requests.Count} optional service handoff request(s) recorded; fulfillment remains with the owning module.");
	}

	private static AdmissionCrmServiceHandoffDto MapServiceHandoff(AdmissionCrmServiceHandoff x) => new()
	{
		Id = x.Id, ActivationId = x.ActivationId, LeadId = x.LeadId, StudentId = x.StudentId,
		Type = x.Type, DestinationModule = x.DestinationModule, ConsentConfirmed = x.ConsentConfirmed,
		Payload = Deserialize<Dictionary<string, string?>>(x.PayloadJson) ?? new(), Status = x.Status,
		AttemptCount = x.AttemptCount, RequestedAt = x.RequestedAt, DispatchedAt = x.DispatchedAt,
		AcknowledgedAt = x.AcknowledgedAt, ExternalReference = x.ExternalReference, LastError = x.LastError
	};

	private static AdmissionCrmNumberSeriesDto MapSeries(AdmissionCrmNumberSeries x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, Name = x.Name, IdentifierType = x.IdentifierType,
		EducationBoardId = x.EducationBoardId, AcademicMediumId = x.AcademicMediumId, StandardId = x.StandardId,
		Prefix = x.Prefix, Suffix = x.Suffix, Padding = x.Padding, StartingSequence = x.StartingSequence, NextSequence = x.NextSequence,
		MaximumSequence = x.MaximumSequence, EffectiveFrom = x.EffectiveFrom, EffectiveUntil = x.EffectiveUntil,
		Status = x.Status, NextPreview = $"{x.Prefix}{x.NextSequence.ToString().PadLeft(x.Padding, '0')}{x.Suffix}",
		ConcurrencyStamp = x.ConcurrencyStamp
	};

	private static AdmissionCrmIdentifierAllocationDto MapIdentifier(AdmissionCrmIdentifierAllocation x) => new()
	{
		Id = x.Id, IdentifierType = x.IdentifierType, Value = x.Value, Sequence = x.Sequence,
		Status = x.Status, AllocatedAt = x.AllocatedAt, CommittedAt = x.CommittedAt,
		IsManual = x.IsManual, AssignedByUserId = x.AssignedByUserId, LifecycleReason = x.LifecycleReason
	};

	private async Task AddTimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type,
		string title, string detail, Guid relatedId) => await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
		GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId, type, title, detail,
		relatedId, CurrentUser.Id, Clock.Now), autoSave: true);

	private static void UpsertStep(List<AdmissionCrmActivationStepDto> steps, string key,
		AdmissionCrmActivationStepStatus status, string detail)
	{
		steps.RemoveAll(x => x.Key == key);
		steps.Add(new AdmissionCrmActivationStepDto { Key = key, Status = status, Detail = detail, UpdatedAt = DateTime.UtcNow });
	}

	private Guid GetCurrentUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}
