using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicEventWorkflow(
	AdmissionCrmPublicAccess access,
	IRepository<AdmissionCrmEvent, Guid> eventRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPublicEventListDto> GetAvailableAsync(
		Guid tenantId,
		string token,
		string accessSessionToken)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, accessSessionToken);
			var now = Clock.Now;
			var query = (await eventRepository.WithDetailsAsync(admissionEvent => admissionEvent.Registrations))
				.Where(admissionEvent =>
					admissionEvent.TenantId == CurrentTenant.Id &&
					admissionEvent.AcademicYearId == lead.AcademicYearId &&
					admissionEvent.AllowFamilySelfScheduling &&
					admissionEvent.Status == AdmissionCrmEventStatus.Published &&
					admissionEvent.StartsAt > now);
			var events = await AsyncExecuter.ToListAsync(query.OrderBy(admissionEvent => admissionEvent.StartsAt));
			return new AdmissionCrmPublicEventListDto
			{
				Events = events.Select(admissionEvent => ToDto(admissionEvent, lead.Id)).ToList()
			};
		}
	}

	public async Task<AdmissionCrmPublicEventDto> ReserveAsync(
		Guid tenantId,
		string token,
		Guid eventId,
		ReserveAdmissionCrmPublicEventDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(
			new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, input.AccessSessionToken);
			var admissionEvent = await GetEventAsync(eventId, lead);
			var registration = admissionEvent.Reserve(
				GuidGenerator.Create(), lead.Id, Clock.Now, "Reserved by the family portal.");
			await AddTimelineAsync(
				lead, AdmissionCrmTimelineEventType.EventRegistrationReserved,
				"Admissions event reserved by family", admissionEvent.Title, registration.Id);
			await eventRepository.UpdateAsync(admissionEvent, autoSave: true);
			await uow.CompleteAsync();
			return ToDto(admissionEvent, lead.Id);
		}
	}

	public async Task<AdmissionCrmPublicEventDto> CancelAsync(
		Guid tenantId,
		string token,
		Guid eventId,
		CancelAdmissionCrmPublicEventRegistrationDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(
			new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, input.AccessSessionToken);
			var admissionEvent = await GetEventAsync(eventId, lead, requireFuture: false);
			var registration = admissionEvent.Registrations.SingleOrDefault(item =>
				item.LeadId == lead.Id && item.Status == AdmissionCrmEventRegistrationStatus.Reserved) ??
				throw new BusinessException("AdmissionCrm:EventRegistrationNotFound");
			admissionEvent.CancelRegistration(
				registration.Id,
				Clock.Now,
				string.IsNullOrWhiteSpace(input.Reason) ? "Cancelled by the family." : input.Reason.Trim());
			await AddTimelineAsync(
				lead, AdmissionCrmTimelineEventType.EventRegistrationCancelled,
				"Admissions event reservation cancelled by family", admissionEvent.Title, registration.Id);
			await eventRepository.UpdateAsync(admissionEvent, autoSave: true);
			await uow.CompleteAsync();
			return ToDto(admissionEvent, lead.Id);
		}
	}

	private async Task<AdmissionCrmEvent> GetEventAsync(
		Guid eventId,
		AdmissionInquiryLead lead,
		bool requireFuture = true)
	{
		var query = await eventRepository.WithDetailsAsync(admissionEvent => admissionEvent.Registrations);
		var admissionEvent = await AsyncExecuter.FirstOrDefaultAsync(query.Where(item =>
			item.Id == eventId && item.TenantId == CurrentTenant.Id &&
			item.AcademicYearId == lead.AcademicYearId && item.AllowFamilySelfScheduling));
		if (admissionEvent == null || admissionEvent.Status != AdmissionCrmEventStatus.Published ||
			(requireFuture && admissionEvent.StartsAt <= Clock.Now))
		{
			throw new BusinessException("AdmissionCrm:EventNotOpenForRegistration");
		}

		return admissionEvent;
	}

	private Task AddTimelineAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmTimelineEventType type,
		string title,
		string details,
		Guid registrationId) =>
		timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
				type, title, details, registrationId, actorUserId: null,
				occurredAt: new DateTimeOffset(Clock.Now)),
			autoSave: false);

	private static AdmissionCrmPublicEventDto ToDto(AdmissionCrmEvent admissionEvent, Guid leadId)
	{
		var registration = admissionEvent.Registrations.Where(item => item.LeadId == leadId)
			.OrderByDescending(item => item.ReservedAt).FirstOrDefault();
		return new AdmissionCrmPublicEventDto
		{
			Id = admissionEvent.Id,
			Title = admissionEvent.Title,
			EventType = admissionEvent.EventType,
			StartsAt = admissionEvent.StartsAt,
			EndsAt = admissionEvent.EndsAt,
			Location = admissionEvent.Location,
			Instructions = admissionEvent.Instructions,
			AvailableCount = Math.Max(0, admissionEvent.Capacity - admissionEvent.ReservedCount),
			RegistrationId = registration?.Id,
			RegistrationStatus = registration?.Status
		};
	}
}
