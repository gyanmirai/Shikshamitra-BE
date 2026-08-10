using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGeneratedDocumentAvailabilityReader(
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmExamSlot, Guid> examSlotRepository,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAsync(AdmissionInquiryLead lead)
	{
		var result = Enum.GetValues<AdmissionCrmGeneratedDocumentType>()
			.Select(type => new AdmissionCrmGeneratedDocumentAvailabilityDto
			{
				DocumentType = type,
				IsAvailable = true,
				Message = "Available"
			})
			.ToList();
		foreach (var item in result)
		{
			try
			{
				await EnsureAvailableAsync(lead, item.DocumentType);
			}
			catch (BusinessException exception) when (GetUnavailableMessage(exception.Code) != null)
			{
				item.IsAvailable = false;
				item.ReasonCode = exception.Code;
				item.Message = GetUnavailableMessage(exception.Code)!;
			}
		}

		return result;
	}

	public async Task<AdmissionCrmHallTicketContext?> EnsureAvailableAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmGeneratedDocumentType documentType)
	{
		if (documentType == AdmissionCrmGeneratedDocumentType.HallTicket)
		{
			return await GetHallTicketContextAsync(lead);
		}

		if (documentType is AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement or
			AdmissionCrmGeneratedDocumentType.CorrectionNotice)
		{
			var queryable = await registrationRepository.GetQueryableAsync();
			var registration = await AsyncExecuter.FirstOrDefaultAsync(queryable
				.Where(item => item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id)
				.OrderByDescending(item => item.IssuedAt));
			if (registration == null || registration.Status is
				AdmissionCrmRegistrationStatus.Revoked or AdmissionCrmRegistrationStatus.Withdrawn)
			{
				throw new BusinessException("AdmissionCrm:RegistrationDocumentUnavailable");
			}

			if (documentType == AdmissionCrmGeneratedDocumentType.CorrectionNotice &&
				registration.Status != AdmissionCrmRegistrationStatus.ReturnedForCorrection)
			{
				throw new BusinessException("AdmissionCrm:CorrectionNoticeNotRequired");
			}

			return null;
		}

		if (documentType == AdmissionCrmGeneratedDocumentType.OfferLetter)
		{
			var queryable = await offerRepository.GetQueryableAsync();
			var offerReady = await AsyncExecuter.AnyAsync(queryable.Where(item =>
				item.TenantId == CurrentTenant.Id &&
				item.LeadId == lead.Id &&
				(item.Status == AdmissionCrmSeatOfferStatus.Approved ||
				 item.Status == AdmissionCrmSeatOfferStatus.Issued ||
				 item.Status == AdmissionCrmSeatOfferStatus.Opened ||
				 item.Status == AdmissionCrmSeatOfferStatus.Accepted)));
			if (!offerReady)
			{
				throw new BusinessException("AdmissionCrm:OfferLetterUnavailable");
			}

			return null;
		}

		var activationQueryable = await activationRepository.GetQueryableAsync();
		var activation = await AsyncExecuter.FirstOrDefaultAsync(activationQueryable.Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id));
		if (documentType == AdmissionCrmGeneratedDocumentType.AdmissionLetter &&
			activation?.Status != AdmissionCrmActivationStatus.FirstDayReady)
		{
			throw new BusinessException("AdmissionCrm:AdmissionLetterUnavailable");
		}

		if (documentType == AdmissionCrmGeneratedDocumentType.CancellationLetter &&
			activation?.Status != AdmissionCrmActivationStatus.Cancelled)
		{
			throw new BusinessException("AdmissionCrm:CancellationLetterUnavailable");
		}

		return null;
	}

	private async Task<AdmissionCrmHallTicketContext> GetHallTicketContextAsync(AdmissionInquiryLead lead)
	{
		var examQueryable = await examRepository.GetQueryableAsync();
		var exam = await AsyncExecuter.FirstOrDefaultAsync(examQueryable
			.Where(item => item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id)
			.OrderByDescending(item => item.ScheduledAt));
		if (exam == null)
		{
			throw new BusinessException("AdmissionCrm:ExamScheduleRequiredForHallTicket")
				.WithData("LeadId", lead.Id);
		}

		var slot = await examSlotRepository.FindAsync(exam.EntranceExamSlotId);
		if (slot != null &&
			slot.TenantId == CurrentTenant.Id &&
			slot.PlaybookId.HasValue &&
			!string.IsNullOrWhiteSpace(slot.ExamDefinitionCode))
		{
			var playbook = await playbookRepository.FindAsync(slot.PlaybookId.Value);
			if (playbook != null && playbook.TenantId == CurrentTenant.Id)
			{
				var definitions = JsonSerializer.Deserialize<List<AdmissionCrmExamDefinitionDto>>(
					playbook.ExamDefinitionsJson,
					JsonOptions) ?? [];
				var definition = definitions.FirstOrDefault(item => string.Equals(
					item.Code,
					slot.ExamDefinitionCode,
					StringComparison.OrdinalIgnoreCase));
				if (definition is { HallTicketEnabled: false })
				{
					throw new BusinessException("AdmissionCrm:HallTicketDisabledForExam")
						.WithData("ExamDefinitionCode", slot.ExamDefinitionCode);
				}
			}
		}

		var venue = !string.IsNullOrWhiteSpace(slot?.RoomOrHall)
			? slot.RoomOrHall
			: !string.IsNullOrWhiteSpace(exam.SlotNameSnapshot)
				? exam.SlotNameSnapshot
				: $"Room {exam.ExamRoomNo}";
		return new AdmissionCrmHallTicketContext(exam.ExamStartTime, exam.ExamEndTime, venue!);
	}

	private static string? GetUnavailableMessage(string? code) => code switch
	{
		"AdmissionCrm:ExamScheduleRequiredForHallTicket" => "Schedule the entrance exam before generating a hall ticket.",
		"AdmissionCrm:HallTicketDisabledForExam" => "This exam uses schedule notification only; no hall ticket is configured.",
		"AdmissionCrm:RegistrationDocumentUnavailable" => "Issue registration before generating registration documents.",
		"AdmissionCrm:CorrectionNoticeNotRequired" => "A correction notice is available only after registration is returned for correction.",
		"AdmissionCrm:OfferLetterUnavailable" => "Approve the seat offer before generating an offer letter.",
		"AdmissionCrm:AdmissionLetterUnavailable" => "Complete student activation before generating an admission letter.",
		"AdmissionCrm:CancellationLetterUnavailable" => "A cancellation letter is available only after activation is cancelled.",
		_ => null
	};
}

public sealed record AdmissionCrmHallTicketContext(DateTimeOffset StartTime, DateTimeOffset EndTime, string Venue);
