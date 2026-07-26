using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Q = QuestPDF.Infrastructure;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmGeneratedDocumentAppService(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmExamSlot, Guid> examSlotRepository,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	StudentScopeAccessService studentScopeAccessService,
	IPdfService pdfService) : ApplicationService, IAdmissionCrmGeneratedDocumentAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmGeneratedDocumentDto> PreviewAsync(
		Guid leadId,
		PreviewAdmissionCrmGeneratedDocumentDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(PreviewAsync));
		var language = NormalizeLanguage(input.Language);
		var generatedAt = Clock.Now;
		return await BuildDocumentAsync(lead, input.DocumentType, language, generatedAt, true, null);
	}

	public async Task<AdmissionCrmGeneratedDocumentDto> IssueAsync(
		Guid leadId,
		IssueAdmissionCrmGeneratedDocumentDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(IssueAsync));
		await AuthorizationService.CheckAsync(RequiredPermission(input.DocumentType));
		var language = NormalizeLanguage(input.Language);
		var generatedAt = Clock.Now;
		var document = await BuildDocumentAsync(lead, input.DocumentType, language, generatedAt, false, input.Reason);
		var timelineEvent = new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.Id,
			lead.AcademicYearId,
			AdmissionCrmTimelineEventType.GeneratedDocumentIssued,
			$"{document.DocumentTypeName} issued",
			$"{document.FileName}. Language: {language}. Reason: {input.Reason ?? "Operational issue"}.",
			null,
			CurrentUser.Id,
			generatedAt);
		await timelineRepository.InsertAsync(timelineEvent, autoSave: true);
		document.TimelineEventId = timelineEvent.Id;
		return document;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAudit)]
	public async Task<IReadOnlyList<AdmissionCrmGeneratedDocumentHistoryDto>> GetHistoryAsync(Guid leadId)
	{
		var lead = await GetLeadAsync(leadId, nameof(GetHistoryAsync));
		var queryable = await timelineRepository.GetQueryableAsync();
		var events = await AsyncExecuter.ToListAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id &&
			item.LeadId == lead.Id &&
			item.EventType == AdmissionCrmTimelineEventType.GeneratedDocumentIssued));
		return events
			.OrderByDescending(item => item.OccurredAt)
			.Select(MapHistory)
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAvailabilityAsync(Guid leadId)
	{
		var lead = await GetLeadAsync(leadId, nameof(GetAvailabilityAsync));
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
				await EnsureDocumentAvailableAsync(lead, item.DocumentType);
			}
			catch (BusinessException exception) when (DocumentUnavailableMessage(exception.Code) != null)
			{
				item.IsAvailable = false;
				item.ReasonCode = exception.Code;
				item.Message = DocumentUnavailableMessage(exception.Code)!;
			}
		}
		return result;
	}

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid leadId, string operation)
	{
		var lead = await leadRepository.FindAsync(leadId);
		if (lead == null || lead.TenantId != CurrentTenant.Id)
		{
			throw new EntityNotFoundException(typeof(AdmissionInquiryLead), leadId);
		}

		await studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	private async Task<AdmissionCrmGeneratedDocumentDto> BuildDocumentAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmGeneratedDocumentType documentType,
		string language,
		DateTimeOffset generatedAt,
		bool isPreview,
		string? reason)
	{
		var documentTypeName = DocumentTypeName(documentType, language);
		var applicantName = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }
			.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
		var hallTicket = await EnsureDocumentAvailableAsync(lead, documentType);
		var lines = new List<ShikshaMitra.Common.PDF.Elements.IElement>
		{
			new TextElement { Text = documentTypeName, FontSize = 18, FontWeight = Q.FontWeight.Bold, Alignment = TextAlignment.Center, Padding = 8, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = Label("Applicant", language) + ": " + (string.IsNullOrWhiteSpace(applicantName) ? "Applicant" : applicantName), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = Label("Contact", language) + ": " + (lead.PrimaryContactNo ?? lead.Email ?? "Not provided"), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = Label("Desired class", language) + ": " + (lead.DesiredClassName ?? "Not specified"), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = Label("Admission intent", language) + ": " + lead.AdmissionIntent, Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = Label("Generated", language) + ": " + generatedAt.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = Label("Reference", language) + ": " + lead.Id.ToString("N")[..12].ToUpperInvariant(), Padding = 4, PaddingUnit = Q.Unit.Point }
		};
		if (hallTicket != null)
		{
			lines.Add(new TextElement { Text = $"Exam date: {hallTicket.StartTime:dd MMM yyyy}", Padding = 4, PaddingUnit = Q.Unit.Point });
			lines.Add(new TextElement { Text = $"Exam time: {hallTicket.StartTime:hh:mm tt} - {hallTicket.EndTime:hh:mm tt}", Padding = 4, PaddingUnit = Q.Unit.Point });
			lines.Add(new TextElement { Text = $"Venue: {hallTicket.Venue}", Padding = 4, PaddingUnit = Q.Unit.Point });
		}
		lines.Add(new TextElement { Text = Instruction(documentType, language), Padding = 12, PaddingUnit = Q.Unit.Point });
		if (!string.IsNullOrWhiteSpace(reason))
		{
			lines.Add(new TextElement { Text = Label("Reason", language) + ": " + reason, Padding = 4, PaddingUnit = Q.Unit.Point });
		}

		var content = await pdfService.GeneratePdfAsBytes(new PDFDocument
		{
			Page = new PageSpecs { BackgroundColor = "#ffffff", Margin = 36, MarginUnit = Q.Unit.Point, DefaultFontSize = 11, PageSize = "A4" },
			Header = new TextElement { Text = "ShikshaMitra Admissions", FontSize = 20, FontWeight = Q.FontWeight.Bold, Color = "#163a5f", Alignment = TextAlignment.Center, Padding = 8, PaddingUnit = Q.Unit.Point },
			Content = new ColumnElement { Elements = lines, Spacing = 6 }
		});
		var fileName = $"admissions-{DocumentSlug(documentType)}-{lead.Id:N}-{generatedAt:yyyyMMddHHmmss}.pdf";
		return new AdmissionCrmGeneratedDocumentDto
		{
			LeadId = lead.Id,
			AcademicYearId = lead.AcademicYearId,
			DocumentType = documentType,
			DocumentTypeName = documentTypeName,
			Language = language,
			FileName = fileName,
			Content = content,
			IsPreview = isPreview,
			GeneratedAt = generatedAt
		};
	}

	private async Task<HallTicketContext?> EnsureDocumentAvailableAsync(
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
			if (!offerReady) throw new BusinessException("AdmissionCrm:OfferLetterUnavailable");
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

	private static string? DocumentUnavailableMessage(string? code) => code switch
	{
		"AdmissionCrm:ExamScheduleRequiredForHallTicket" =>
			"Schedule the entrance exam before generating a hall ticket.",
		"AdmissionCrm:HallTicketDisabledForExam" =>
			"This exam uses schedule notification only; no hall ticket is configured.",
		"AdmissionCrm:RegistrationDocumentUnavailable" =>
			"Issue registration before generating registration documents.",
		"AdmissionCrm:CorrectionNoticeNotRequired" =>
			"A correction notice is available only after registration is returned for correction.",
		"AdmissionCrm:OfferLetterUnavailable" =>
			"Approve the seat offer before generating an offer letter.",
		"AdmissionCrm:AdmissionLetterUnavailable" =>
			"Complete student activation before generating an admission letter.",
		"AdmissionCrm:CancellationLetterUnavailable" =>
			"A cancellation letter is available only after activation is cancelled.",
		_ => null
	};

	private async Task<HallTicketContext> GetHallTicketContextAsync(AdmissionInquiryLead lead)
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
		return new HallTicketContext(exam.ExamStartTime, exam.ExamEndTime, venue!);
	}

	private sealed record HallTicketContext(DateTimeOffset StartTime, DateTimeOffset EndTime, string Venue);

	private static AdmissionCrmGeneratedDocumentHistoryDto MapHistory(AdmissionCrmTimelineEvent item)
	{
		var details = item.Details ?? string.Empty;
		var documentType = Enum.GetValues<AdmissionCrmGeneratedDocumentType>()
			.FirstOrDefault(value => details.Contains(DocumentSlug(value), StringComparison.OrdinalIgnoreCase));
		var language = details.Contains("Language: gu", StringComparison.OrdinalIgnoreCase) ? "gu" :
			details.Contains("Language: hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
		var fileName = details.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "generated-document";
		return new AdmissionCrmGeneratedDocumentHistoryDto
		{
			TimelineEventId = item.Id,
			LeadId = item.LeadId,
			DocumentType = documentType,
			DocumentTypeName = DocumentTypeName(documentType, language),
			FileName = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".pdf",
			Language = language,
			Reason = details,
			GeneratedAt = item.OccurredAt,
			ActorUserId = item.ActorUserId
		};
	}

	private static string RequiredPermission(AdmissionCrmGeneratedDocumentType documentType) => documentType switch
	{
		AdmissionCrmGeneratedDocumentType.HallTicket => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam,
		AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement or AdmissionCrmGeneratedDocumentType.CorrectionNotice => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks,
		AdmissionCrmGeneratedDocumentType.OfferLetter => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers,
		AdmissionCrmGeneratedDocumentType.AdmissionLetter => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation,
		AdmissionCrmGeneratedDocumentType.CancellationLetter => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission,
		_ => ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View
	};

	private static string NormalizeLanguage(string? language)
	{
		var normalized = (language ?? "en").Trim().ToLowerInvariant();
		return normalized is "en" or "gu" or "hi"
			? normalized
			: throw new BusinessException("AdmissionCrm:UnsupportedDocumentLanguage").WithData("Language", language);
	}

	private static string DocumentSlug(AdmissionCrmGeneratedDocumentType documentType) => documentType switch
	{
		AdmissionCrmGeneratedDocumentType.HallTicket => "hall-ticket",
		AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "registration-acknowledgement",
		AdmissionCrmGeneratedDocumentType.OfferLetter => "offer-letter",
		AdmissionCrmGeneratedDocumentType.AdmissionLetter => "admission-letter",
		AdmissionCrmGeneratedDocumentType.CorrectionNotice => "correction-notice",
		AdmissionCrmGeneratedDocumentType.CancellationLetter => "cancellation-letter",
		_ => "admission-document"
	};

	private static string DocumentTypeName(AdmissionCrmGeneratedDocumentType documentType, string language) =>
		language switch
		{
			"gu" => documentType switch
			{
				AdmissionCrmGeneratedDocumentType.HallTicket => "Exam Hall Ticket",
				AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Registration Acknowledgement",
				AdmissionCrmGeneratedDocumentType.OfferLetter => "Seat Offer Letter",
				AdmissionCrmGeneratedDocumentType.AdmissionLetter => "Admission Letter",
				AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Correction Notice",
				_ => "Cancellation Letter"
			},
			"hi" => documentType switch
			{
				AdmissionCrmGeneratedDocumentType.HallTicket => "Exam Hall Ticket",
				AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Registration Acknowledgement",
				AdmissionCrmGeneratedDocumentType.OfferLetter => "Seat Offer Letter",
				AdmissionCrmGeneratedDocumentType.AdmissionLetter => "Admission Letter",
				AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Correction Notice",
				_ => "Cancellation Letter"
			},
			_ => documentType switch
			{
				AdmissionCrmGeneratedDocumentType.HallTicket => "Entrance Exam Hall Ticket",
				AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Registration Acknowledgement",
				AdmissionCrmGeneratedDocumentType.OfferLetter => "Seat Offer Letter",
				AdmissionCrmGeneratedDocumentType.AdmissionLetter => "Admission Letter",
				AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Registration Correction Notice",
				_ => "Admission Cancellation Letter"
			}
		};

	private static string Label(string label, string language) => language switch
	{
		"gu" => label switch { "Applicant" => "Applicant", "Contact" => "Contact", "Desired class" => "Class", "Admission intent" => "Intent", "Generated" => "Generated", "Reference" => "Reference", "Reason" => "Reason", _ => label },
		"hi" => label switch { "Applicant" => "Applicant", "Contact" => "Contact", "Desired class" => "Class", "Admission intent" => "Intent", "Generated" => "Generated", "Reference" => "Reference", "Reason" => "Reason", _ => label },
		_ => label
	};

	private static string Instruction(AdmissionCrmGeneratedDocumentType documentType, string language) => documentType switch
	{
		AdmissionCrmGeneratedDocumentType.HallTicket => "Please carry this hall ticket and the requested original identity documents on exam day.",
		AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement => "Keep this acknowledgement for future communication with the admissions office.",
		AdmissionCrmGeneratedDocumentType.OfferLetter => "Review the seat offer and respond before the configured expiry date.",
		AdmissionCrmGeneratedDocumentType.AdmissionLetter => "This letter confirms the admissions handoff. Complete any remaining school instructions before the first day.",
		AdmissionCrmGeneratedDocumentType.CorrectionNotice => "Please correct the listed registration information and resubmit through the secure applicant link.",
		_ => "This cancellation is recorded in the admissions timeline. Contact the admissions office for questions."
	};
}
