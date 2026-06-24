using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.Communication;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Communication;

public partial class CalendarCommunicationAppService : ApplicationService, ICalendarCommunicationAppService
{
	private const string CalendarEventSourceType = "CalendarEvent";
	private const string CalendarHolidaySourceType = "CalendarHoliday";

	private static readonly Regex TemplateVariableRegex = new(
		@"\{\{\s*(?<key>[^{}]+?)\s*\}\}",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private readonly IEventAppService _eventAppService;
	private readonly IHolidayAppService _holidayAppService;
	private readonly ICommunicationAppService _communicationAppService;

	public CalendarCommunicationAppService(
		IEventAppService eventAppService,
		IHolidayAppService holidayAppService,
		ICommunicationAppService communicationAppService)
	{
		_eventAppService = eventAppService;
		_holidayAppService = holidayAppService;
		_communicationAppService = communicationAppService;
	}

	public async Task<CalendarCommunicationResultDto> AnnounceEventAsync(Guid eventId, CalendarCommunicationRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.HR.Calendar.View);

		var calendarEvent = await _eventAppService.GetAsync(eventId);
		var audience = BuildAudience(input.AudienceDefinition, input.IncludeSourceAudience ? calendarEvent.AudienceIds : []);
		var variables = BuildEventVariables(calendarEvent);
		var (subject, body) = await ResolveContentAsync(
			input,
			$"Event: {calendarEvent.Title}",
			BuildEventBody(calendarEvent),
			variables);

		var attachments = input.Attachments.ToList();
		if (!calendarEvent.Poster.IsNullOrWhiteSpace())
		{
			attachments.Add(new CommunicationAttachmentDto
			{
				Name = "Event poster",
				Url = calendarEvent.Poster!
			});
		}

		var campaign = await CreateCalendarCampaignAsync(
			input,
			CalendarEventSourceType,
			eventId,
			calendarEvent.Title,
			calendarEvent.AcademicYearId,
			audience,
			subject,
			body,
			input.LinkUrl ?? $"/calendar/events/{eventId}",
			attachments);

		return await PublishIfRequestedAsync(campaign, input.PublishImmediately, CalendarEventSourceType, eventId);
	}

	public async Task<CalendarCommunicationResultDto> AnnounceHolidayAsync(Guid holidayId, CalendarCommunicationRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.HR.Calendar.View);

		var holiday = await _holidayAppService.GetAsync(holidayId);
		var audience = BuildAudience(input.AudienceDefinition, input.IncludeSourceAudience ? holiday.AudienceIds : []);
		var variables = BuildHolidayVariables(holiday);
		var (subject, body) = await ResolveContentAsync(
			input,
			$"Holiday: {holiday.HolidayName}",
			BuildHolidayBody(holiday),
			variables);

		var attachments = input.Attachments
			.Concat(ParseHolidayAttachments(holiday.Attachments))
			.ToList();

		var campaign = await CreateCalendarCampaignAsync(
			input,
			CalendarHolidaySourceType,
			holidayId,
			holiday.HolidayName,
			holiday.AcademicYearId,
			audience,
			subject,
			body,
			input.LinkUrl ?? $"/calendar/holidays/{holidayId}",
			attachments);

		return await PublishIfRequestedAsync(campaign, input.PublishImmediately, CalendarHolidaySourceType, holidayId);
	}

	private async Task<CommunicationCampaignDto> CreateCalendarCampaignAsync(
		CalendarCommunicationRequestDto input,
		string sourceEntityType,
		Guid sourceEntityId,
		string sourceReference,
		Guid academicYearId,
		CommunicationAudienceDefinitionDto audience,
		string subject,
		string body,
		string linkUrl,
		List<CommunicationAttachmentDto> attachments)
	{
		var channels = input.Channels.Distinct().ToList();
		var fallbackOrder = input.FallbackOrder.Count > 0
			? input.FallbackOrder.Distinct().ToList()
			: channels;

		return await _communicationAppService.CreateCampaignAsync(new CreateUpdateCommunicationCampaignDto
		{
			AcademicYearId = academicYearId,
			SourceModule = CommunicationSourceModule.Calendar,
			SourceEntityType = sourceEntityType,
			SourceEntityId = sourceEntityId,
			SourceReference = sourceReference,
			CampaignType = input.CampaignType.IsNullOrWhiteSpace() ? "Announcement" : input.CampaignType.Trim(),
			Title = subject,
			Subject = subject,
			Body = body,
			ImportanceLevel = input.ImportanceLevel,
			ApprovalRequired = input.ApprovalRequired,
			TemplateId = input.TemplateId,
			AudienceDefinition = audience,
			Channels = channels,
			FallbackOrder = fallbackOrder,
			Attachments = attachments,
			LinkUrl = linkUrl,
			LanguageCode = input.LanguageCode.IsNullOrWhiteSpace() ? "en" : input.LanguageCode.Trim(),
			ScheduledAt = input.ScheduledAt,
			RequiresAcknowledgement = input.RequiresAcknowledgement
		});
	}

	private async Task<CalendarCommunicationResultDto> PublishIfRequestedAsync(
		CommunicationCampaignDto campaign,
		bool publishImmediately,
		string sourceEntityType,
		Guid sourceEntityId)
	{
		var published = false;
		if (publishImmediately && campaign.Status != CommunicationCampaignStatus.PendingApproval)
		{
			campaign = await _communicationAppService.PublishCampaignAsync(campaign.Id);
			published = campaign.PublishedAt.HasValue
				|| campaign.Status is CommunicationCampaignStatus.Queued
					or CommunicationCampaignStatus.Sending
					or CommunicationCampaignStatus.Sent
					or CommunicationCampaignStatus.PartiallyFailed
					or CommunicationCampaignStatus.Failed;
		}

		return new CalendarCommunicationResultDto
		{
			Campaign = campaign,
			Published = published,
			ApprovalRequired = campaign.ApprovalRequired || campaign.Status == CommunicationCampaignStatus.PendingApproval,
			SourceEntityType = sourceEntityType,
			SourceEntityId = sourceEntityId
		};
	}

	private async Task<(string Subject, string Body)> ResolveContentAsync(
		CalendarCommunicationRequestDto input,
		string defaultSubject,
		string defaultBody,
		Dictionary<string, string> variables)
	{
		var subject = input.Subject.IsNullOrWhiteSpace() ? defaultSubject : input.Subject!;
		var body = input.Body.IsNullOrWhiteSpace() ? defaultBody : input.Body!;

		if (input.TemplateId.HasValue)
		{
			var template = await _communicationAppService.GetTemplateAsync(input.TemplateId.Value);
			subject = input.Subject.IsNullOrWhiteSpace() ? template.Subject : subject;
			body = input.Body.IsNullOrWhiteSpace() ? template.Body : body;
		}

		return (ReplaceVariables(subject, variables), ReplaceVariables(body, variables));
	}

	private static CommunicationAudienceDefinitionDto BuildAudience(
		CommunicationAudienceDefinitionDto input,
		IEnumerable<Guid> sourceGroupIds)
	{
		return new CommunicationAudienceDefinitionDto
		{
			GroupIds = input.GroupIds.Concat(sourceGroupIds).Distinct().ToList(),
			OrganizationIds = input.OrganizationIds.Distinct().ToList(),
			ClassIds = input.ClassIds.Distinct().ToList(),
			DivisionIds = input.DivisionIds.Distinct().ToList(),
			StudentIds = input.StudentIds.Distinct().ToList(),
			ParentGuardianIds = input.ParentGuardianIds.Distinct().ToList(),
			EmployeeIds = input.EmployeeIds.Distinct().ToList(),
			UserIds = input.UserIds.Distinct().ToList(),
			Roles = input.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
			Filters = input.Filters.ToList(),
			ExplicitRecipients = input.ExplicitRecipients.ToList(),
			ParentsFirst = input.ParentsFirst,
			IncludeStudentsWhenEligible = input.IncludeStudentsWhenEligible
		};
	}

	private static Dictionary<string, string> BuildEventVariables(EventDto calendarEvent)
	{
		var startDate = FormatDate(calendarEvent.StartDate ?? calendarEvent.MeetingDate);
		var endDate = FormatDate(calendarEvent.EndDate ?? calendarEvent.MeetingDate);

		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["title"] = calendarEvent.Title,
			["eventTitle"] = calendarEvent.Title,
			["startDate"] = startDate,
			["endDate"] = endDate,
			["meetingDate"] = FormatDate(calendarEvent.MeetingDate),
			["startTime"] = calendarEvent.StartTime,
			["endTime"] = calendarEvent.EndTime,
			["venue"] = calendarEvent.Venue ?? string.Empty,
			["mode"] = calendarEvent.Mode.ToString(),
			["description"] = calendarEvent.Description ?? string.Empty
		};
	}

	private static Dictionary<string, string> BuildHolidayVariables(HolidayDto holiday)
	{
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["title"] = holiday.HolidayName,
			["holidayName"] = holiday.HolidayName,
			["startDate"] = holiday.StartDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
			["endDate"] = holiday.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
			["description"] = holiday.Description ?? string.Empty
		};
	}

	private static string BuildEventBody(EventDto calendarEvent)
	{
		var details = new List<string>
		{
			calendarEvent.Title,
			$"Date: {FormatDate(calendarEvent.StartDate ?? calendarEvent.MeetingDate)}"
		};

		if (calendarEvent.EndDate.HasValue && calendarEvent.StartDate?.Date != calendarEvent.EndDate.Value.Date)
		{
			details.Add($"Ends: {FormatDate(calendarEvent.EndDate)}");
		}

		if (!calendarEvent.StartTime.IsNullOrWhiteSpace() || !calendarEvent.EndTime.IsNullOrWhiteSpace())
		{
			details.Add($"Time: {calendarEvent.StartTime} - {calendarEvent.EndTime}".TrimEnd(' ', '-'));
		}

		if (!calendarEvent.Venue.IsNullOrWhiteSpace())
		{
			details.Add($"Venue: {calendarEvent.Venue}");
		}

		if (!calendarEvent.Description.IsNullOrWhiteSpace())
		{
			details.Add(calendarEvent.Description!);
		}

		return string.Join(Environment.NewLine, details);
	}

	private static string BuildHolidayBody(HolidayDto holiday)
	{
		var details = new List<string>
		{
			holiday.HolidayName,
			$"Date: {holiday.StartDate:dd MMM yyyy}"
		};

		if (holiday.StartDate != holiday.EndDate)
		{
			details.Add($"Ends: {holiday.EndDate:dd MMM yyyy}");
		}

		if (!holiday.Description.IsNullOrWhiteSpace())
		{
			details.Add(holiday.Description!);
		}

		return string.Join(Environment.NewLine, details);
	}

	private static List<CommunicationAttachmentDto> ParseHolidayAttachments(string? attachments)
	{
		if (attachments.IsNullOrWhiteSpace())
		{
			return [];
		}

		return attachments
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(url => new CommunicationAttachmentDto
			{
				Name = url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Holiday attachment",
				Url = url
			})
			.ToList();
	}

	private static string ReplaceVariables(string value, Dictionary<string, string> variables)
	{
		return TemplateVariableRegex.Replace(value, match =>
		{
			var key = match.Groups["key"].Value.Trim();
			return variables.TryGetValue(key, out var replacement) ? replacement : match.Value;
		});
	}

	private static string FormatDate(DateTimeOffset? value)
	{
		return value.HasValue
			? value.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
			: string.Empty;
	}
}
