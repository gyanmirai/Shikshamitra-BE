using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Notifications;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcademicNotificationScopeDto(Guid? AcademicYearId = null, Guid? BoardId = null,
	Guid? MediumId = null, Guid? ClassId = null, Guid? DivisionId = null);

public sealed class GetAcademicNotificationRulesDto : PagedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? ClassId { get; set; }
	public Guid? DivisionId { get; set; }
	public string? EventKey { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcademicNotificationRuleTimingDto(int DelayMinutes = 0, int ReminderSchoolDaysBefore = 1,
	TimeOnly? ReminderLocalTime = null)
{
	public int? ReviewerEscalationMinutes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcademicNotificationQuietHoursDto(bool Enabled = true, TimeOnly? Start = null,
	TimeOnly? End = null, bool Bypass = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveAcademicNotificationRuleDto
{
	public required string EventKey { get; init; }
	public required AcademicNotificationScopeDto Scope { get; init; }
	public bool Enabled { get; init; }
	public NotificationGuardianMode GuardianMode { get; init; } = NotificationGuardianMode.PrimaryOnly;
	public IReadOnlyList<string> AdditionalGuardianRelationships { get; init; } = [];
	public bool IncludeStudentAccount { get; init; }
	public IReadOnlyList<string> StaffAudienceSelectors { get; init; } = [];
	public IReadOnlyList<CommunicationChannelType> Channels { get; init; } = [CommunicationChannelType.InApp];
	public IReadOnlyList<Guid> TemplateIds { get; init; } = [];
	public AcademicNotificationRuleTimingDto Timing { get; init; } = new();
	public AcademicNotificationQuietHoursDto QuietHours { get; init; } = new();
	public string FallbackLanguage { get; init; } = "en";
	public string? ConcurrencyStamp { get; init; }
}

public sealed record AcademicNotificationRuleDto(Guid Id, string EventKey, AcademicNotificationScopeDto Scope,
	bool Enabled, int Revision, string ConcurrencyStamp, NotificationRuleRuntimeConfiguration? Configuration);

public sealed record AcademicNotificationRuleListItemDto(string EventKey, AcademicNotificationScopeDto RequestedScope,
	AcademicNotificationRuleDto? EffectiveRule, bool IsInherited);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewAcademicNotificationRuleDto(string SourceType, Guid SourceId, SaveAcademicNotificationRuleDto Rule);

public sealed record AcademicNotificationPreviewExclusionDto(string Code, int Count);

public sealed record AcademicNotificationPreviewChannelDto(CommunicationChannelType Channel, int EligibleRecipientCount,
	int ReadyDeliveryCount, IReadOnlyList<AcademicNotificationPreviewExclusionDto> Exclusions);

public sealed record AcademicNotificationPreviewSampleDto(Guid TemplateId, Guid RevisionId, string RecipientKind,
	CommunicationChannelType Channel, string LanguageCode, bool IsSynthetic, NotificationRenderResult Rendered);

public sealed record AcademicNotificationRulePreviewDto(int EligibleRecipientCount,
	IReadOnlyList<AcademicNotificationPreviewChannelDto> Channels, IReadOnlyList<AcademicNotificationPreviewSampleDto> Samples,
	IReadOnlyList<string> BlockingIssues);

public interface IAcademicNotificationRuleAppService : IApplicationService
{
	Task<PagedResultDto<AcademicNotificationRuleListItemDto>> GetListAsync(GetAcademicNotificationRulesDto input,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationRuleDto> SaveAsync(string eventKey, SaveAcademicNotificationRuleDto input,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationRulePreviewDto> PreviewAsync(string eventKey, PreviewAcademicNotificationRuleDto input,
		CancellationToken cancellationToken = default);
}
