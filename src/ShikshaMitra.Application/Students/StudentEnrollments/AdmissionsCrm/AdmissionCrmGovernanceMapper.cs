using System.Collections.Generic;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmGovernanceMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmRetentionPolicyDto Retention(AdmissionCrmRetentionPolicy x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, Name = x.Name, Category = x.Category,
		Action = x.Action, RetainDays = x.RetainDays, Scope = Deserialize<Dictionary<string, string?>>(x.ScopeJson) ?? new(),
		AuthorityNote = x.AuthorityNote, Status = x.Status, SubmittedByUserId = x.SubmittedByUserId,
		ApprovedByUserId = x.ApprovedByUserId, PublishedAt = x.PublishedAt
	};

	public static AdmissionCrmPrivacyRequestDto Privacy(AdmissionCrmPrivacyRequest x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, LeadId = x.LeadId, Type = x.Type, Reason = x.Reason,
		Preview = Deserialize<AdmissionCrmPrivacyPreviewDto>(x.PreviewJson) ?? new(), Status = x.Status,
		RequestedByUserId = x.RequestedByUserId, RequestedAt = x.RequestedAt, DecidedByUserId = x.DecidedByUserId,
		DecidedAt = x.DecidedAt, DecisionReason = x.DecisionReason, CompletionReference = x.CompletionReference, CompletedAt = x.CompletedAt
	};

	public static AdmissionCrmExportRequestDto Export(AdmissionCrmExportRequest x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, ReportType = x.ReportType, DefinitionVersion = x.DefinitionVersion,
		Filters = Deserialize<Dictionary<string, string?>>(x.FiltersJson) ?? new(), FileName = x.FileName, Status = x.Status,
		RequestedByUserId = x.RequestedByUserId, RequestedAt = x.RequestedAt, DataAsOf = x.DataAsOf,
		ExpiresAt = x.ExpiresAt, DownloadCount = x.DownloadCount, LastDownloadedAt = x.LastDownloadedAt
	};

	public static AdmissionCrmReportScheduleDto Schedule(AdmissionCrmReportSchedule x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Name = x.Name, ReportType = x.ReportType, Frequency = x.Frequency,
		Destination = x.Destination, Filters = Deserialize<Dictionary<string, string?>>(x.FiltersJson) ?? new(),
		NextRunAt = x.NextRunAt, IsActive = x.IsActive, LastRunAt = x.LastRunAt
	};

	public static AdmissionCrmWorkDelegationDto Delegation(AdmissionCrmWorkDelegation x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, DelegatorUserId = x.DelegatorUserId, DelegateUserId = x.DelegateUserId,
		QueueTypes = Deserialize<IReadOnlyList<AdmissionCrmOperationsQueueType>>(x.QueueTypesJson) ?? [], StartsAt = x.StartsAt,
		EndsAt = x.EndsAt, Reason = x.Reason, Status = x.Status
	};

	public static AdmissionCrmTimelineEventDto Timeline(AdmissionCrmTimelineEvent x) => new()
	{
		Id = x.Id, LeadId = x.LeadId, AcademicYearId = x.AcademicYearId, EventType = x.EventType,
		Title = x.Title, Details = x.Details, RelatedEntityId = x.RelatedEntityId, ActorUserId = x.ActorUserId, OccurredAt = x.OccurredAt
	};

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}
