using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEventAppService : ApplicationService, IAdmissionCrmEventAppService
{
	private readonly IRepository<AdmissionCrmEvent, Guid> _eventRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _scopeAccess;
	private readonly AdmissionCrmStaffEligibilityService _staffEligibilityService;

	public AdmissionCrmEventAppService(
		IRepository<AdmissionCrmEvent, Guid> eventRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService scopeAccess,
		AdmissionCrmStaffEligibilityService staffEligibilityService)
	{
		_eventRepository = eventRepository;
		_leadRepository = leadRepository;
		_timelineRepository = timelineRepository;
		_scopeAccess = scopeAccess;
		_staffEligibilityService = staffEligibilityService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewEvents)]
	public async Task<PagedResultDto<AdmissionCrmEventDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmEventListInput input)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetListAsync));
		var query = (await _eventRepository.WithDetailsAsync(x => x.Registrations))
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			query = query.Where(x => x.Title.ToLower().Contains(filter) || x.Location.ToLower().Contains(filter));
		}
		if (input.EventType.HasValue) query = query.Where(x => x.EventType == input.EventType);
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status);
		if (input.StartsFrom.HasValue) query = query.Where(x => x.StartsAt >= input.StartsFrom);
		if (input.StartsUntil.HasValue) query = query.Where(x => x.StartsAt < input.StartsUntil.Value.AddDays(1));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var descending = input.Sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var sortField = input.Sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()?.ToLowerInvariant();
		var ordered = sortField switch
		{
			"title" => descending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
			"location" => descending ? query.OrderByDescending(x => x.Location) : query.OrderBy(x => x.Location),
			"eventtype" => descending ? query.OrderByDescending(x => x.EventType) : query.OrderBy(x => x.EventType),
			"status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
			_ => descending ? query.OrderByDescending(x => x.StartsAt) : query.OrderBy(x => x.StartsAt)
		};
		var events = await AsyncExecuter.ToListAsync(ordered
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		var leads = await GetAccessibleLeadsAsync(academicYearId, events.SelectMany(x => x.Registrations).Select(x => x.LeadId));
		return new PagedResultDto<AdmissionCrmEventDto>(totalCount, events.Select(x => ToDto(x, leads)).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewEvents)]
	public async Task<AdmissionCrmEventDto> GetAsync(Guid eventId)
	{
		var admissionEvent = await GetEventAsync(eventId);
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(admissionEvent.AcademicYearId, nameof(GetAsync));
		var leads = await GetAccessibleLeadsAsync(admissionEvent.AcademicYearId, admissionEvent.Registrations.Select(x => x.LeadId));
		return ToDto(admissionEvent, leads);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> CreateAsync(Guid academicYearId, CreateUpdateAdmissionCrmEventDto input)
	{
		await EnsureManageScopeAsync(academicYearId, nameof(CreateAsync));
		await _staffEligibilityService.EnsureOwnerEligibleAsync(input.OwnerUserId, academicYearId);
		var admissionEvent = new AdmissionCrmEvent(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Title, input.EventType, input.StartsAt, input.EndsAt, input.Location, input.Capacity,
			input.OwnerUserId, input.Instructions, input.AllowFamilySelfScheduling);
		await _eventRepository.InsertAsync(admissionEvent, autoSave: true);
		return ToDto(admissionEvent, new Dictionary<Guid, AdmissionInquiryLead>());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> UpdateAsync(Guid eventId, CreateUpdateAdmissionCrmEventDto input)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, nameof(UpdateAsync));
		if (admissionEvent.Status == AdmissionCrmEventStatus.Published && input.StartsAt <= Clock.Now)
		{
			throw new BusinessException("AdmissionCrm:PublishedEventMustStartInFuture");
		}
		await _staffEligibilityService.EnsureOwnerEligibleAsync(input.OwnerUserId, admissionEvent.AcademicYearId);
		admissionEvent.UpdateDetails(input.Title, input.EventType, input.StartsAt, input.EndsAt, input.Location,
			input.Capacity, input.OwnerUserId, input.Instructions, input.AllowFamilySelfScheduling);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> PublishAsync(Guid eventId)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, nameof(PublishAsync));
		admissionEvent.Publish(Clock.Now);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> CancelAsync(Guid eventId, CancelAdmissionCrmEventDto input)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, nameof(CancelAsync));
		admissionEvent.Cancel(Clock.Now, input.Reason);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> CompleteAsync(Guid eventId)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, nameof(CompleteAsync));
		admissionEvent.Complete(Clock.Now);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> ReserveAsync(Guid eventId, ReserveAdmissionCrmEventRegistrationDto input)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, nameof(ReserveAsync));
		var lead = await GetLeadAsync(input.LeadId, admissionEvent.AcademicYearId);
		var registration = admissionEvent.Reserve(GuidGenerator.Create(), lead.Id, Clock.Now, input.Notes);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.EventRegistrationReserved,
			"Admissions event reserved", admissionEvent.Title, registration.Id);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public async Task<AdmissionCrmEventDto> CancelRegistrationAsync(
		Guid eventId, Guid registrationId, CancelAdmissionCrmEventRegistrationDto input)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, nameof(CancelRegistrationAsync));
		var registration = admissionEvent.Registrations.SingleOrDefault(x => x.Id == registrationId)
			?? throw new BusinessException("AdmissionCrm:EventRegistrationNotFound");
		var lead = await GetLeadAsync(registration.LeadId, admissionEvent.AcademicYearId);
		admissionEvent.CancelRegistration(registrationId, Clock.Now, input.Reason);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.EventRegistrationCancelled,
			"Admissions event reservation cancelled", admissionEvent.Title, registration.Id);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CheckInEvents)]
	public Task<AdmissionCrmEventDto> CheckInAsync(Guid eventId, Guid registrationId) =>
		RecordAttendanceAsync(eventId, registrationId, true);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CheckInEvents)]
	public Task<AdmissionCrmEventDto> MarkNoShowAsync(Guid eventId, Guid registrationId) =>
		RecordAttendanceAsync(eventId, registrationId, false);

	private async Task<AdmissionCrmEventDto> RecordAttendanceAsync(Guid eventId, Guid registrationId, bool checkedIn)
	{
		var admissionEvent = await GetEventForManagementAsync(eventId, checkedIn ? nameof(CheckInAsync) : nameof(MarkNoShowAsync));
		var registration = admissionEvent.Registrations.SingleOrDefault(x => x.Id == registrationId)
			?? throw new BusinessException("AdmissionCrm:EventRegistrationNotFound");
		var lead = await GetLeadAsync(registration.LeadId, admissionEvent.AcademicYearId);
		if (checkedIn) admissionEvent.CheckIn(registrationId, Clock.Now);
		else admissionEvent.MarkNoShow(registrationId, Clock.Now);
		await AddTimelineAsync(lead,
			checkedIn ? AdmissionCrmTimelineEventType.EventRegistrationCheckedIn : AdmissionCrmTimelineEventType.EventRegistrationMarkedNoShow,
			checkedIn ? "Admissions event check-in recorded" : "Admissions event no-show recorded",
			admissionEvent.Title, registration.Id);
		await _eventRepository.UpdateAsync(admissionEvent, autoSave: true);
		return await ToManagementDtoAsync(admissionEvent);
	}

	private async Task<AdmissionCrmEvent> GetEventAsync(Guid eventId)
	{
		var query = await _eventRepository.WithDetailsAsync(x => x.Registrations);
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == eventId && x.TenantId == CurrentTenant.Id))
			?? throw new BusinessException("AdmissionCrm:EventNotFound").WithData("EventId", eventId);
	}

	private async Task<AdmissionCrmEvent> GetEventForManagementAsync(Guid eventId, string operation)
	{
		var admissionEvent = await GetEventAsync(eventId);
		await EnsureManageScopeAsync(admissionEvent.AcademicYearId, operation);
		return admissionEvent;
	}

	private Task EnsureManageScopeAsync(Guid academicYearId, string operation) =>
		_scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, operation);

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid leadId, Guid academicYearId)
	{
		var lead = await _leadRepository.FindAsync(leadId);
		if (lead == null || lead.TenantId != CurrentTenant.Id || lead.AcademicYearId != academicYearId)
			throw new BusinessException("AdmissionCrm:LeadNotFound").WithData("LeadId", leadId);
		return lead;
	}

	private async Task<Dictionary<Guid, AdmissionInquiryLead>> GetAccessibleLeadsAsync(Guid academicYearId, IEnumerable<Guid> leadIds)
	{
		var ids = leadIds.Distinct().ToList();
		if (ids.Count == 0) return [];
		var query = (await _leadRepository.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && ids.Contains(x.Id));
		query = await _scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		return (await AsyncExecuter.ToListAsync(query)).ToDictionary(x => x.Id);
	}

	private async Task<AdmissionCrmEventDto> ToManagementDtoAsync(AdmissionCrmEvent admissionEvent) =>
		ToDto(admissionEvent, await GetAccessibleLeadsAsync(admissionEvent.AcademicYearId,
			admissionEvent.Registrations.Select(x => x.LeadId)));

	private static AdmissionCrmEventDto ToDto(AdmissionCrmEvent admissionEvent, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var registrations = admissionEvent.Registrations.Where(x => leads.ContainsKey(x.LeadId)).Select(x =>
		{
			var lead = leads[x.LeadId];
			return new AdmissionCrmEventRegistrationDto
			{
				Id = x.Id, LeadId = x.LeadId, ApplicantName = BuildApplicantName(lead),
				ParentGuardianName = lead.ParentGuardianName, PrimaryContactNo = lead.PrimaryContactNo,
				Status = x.Status, ReservedAt = x.ReservedAt, CheckedInAt = x.CheckedInAt,
				CancelledAt = x.CancelledAt, NoShowRecordedAt = x.NoShowRecordedAt,
				Notes = x.Notes, CancellationReason = x.CancellationReason
			};
		}).ToList();
		return new AdmissionCrmEventDto
		{
			Id = admissionEvent.Id, AcademicYearId = admissionEvent.AcademicYearId, Title = admissionEvent.Title,
			EventType = admissionEvent.EventType, StartsAt = admissionEvent.StartsAt, EndsAt = admissionEvent.EndsAt,
			Location = admissionEvent.Location, Capacity = admissionEvent.Capacity,
			ReservedCount = admissionEvent.ReservedCount, AvailableCount = Math.Max(0, admissionEvent.Capacity - admissionEvent.ReservedCount),
			OwnerUserId = admissionEvent.OwnerUserId, Instructions = admissionEvent.Instructions,
			AllowFamilySelfScheduling = admissionEvent.AllowFamilySelfScheduling,
			Status = admissionEvent.Status, PublishedAt = admissionEvent.PublishedAt,
			CancelledAt = admissionEvent.CancelledAt, CancellationReason = admissionEvent.CancellationReason,
			Registrations = registrations
		};
	}

	private static string BuildApplicantName(AdmissionInquiryLead lead)
	{
		var name = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }
			.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
		return string.IsNullOrWhiteSpace(name)
			? lead.ParentGuardianName ?? lead.PrimaryContactNo ?? lead.Email ?? "Unnamed applicant"
			: name;
	}

	private async Task AddTimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type,
		string title, string details, Guid relatedId)
	{
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id,
			lead.Id, lead.AcademicYearId, type, title, details, relatedId, CurrentUser.Id,
			new DateTimeOffset(Clock.Now)), autoSave: false);
	}
}
