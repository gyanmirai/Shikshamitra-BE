using System.Collections.Generic;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmRouteMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmRouteDefinitionDto Map(AdmissionCrmRouteDefinition entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		Code = entity.Code,
		Name = entity.Name,
		RouteType = entity.RouteType,
		Version = entity.Version,
		Status = entity.Status,
		Configuration = Deserialize<AdmissionCrmRouteConfigurationDto>(entity.ConfigurationJson) ?? new(),
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveUntil = entity.EffectiveUntil
	};

	public static AdmissionCrmRouteAssignmentDto Map(AdmissionCrmRouteAssignment entity) => new()
	{
		Id = entity.Id,
		LeadId = entity.LeadId,
		RouteDefinitionId = entity.RouteDefinitionId,
		RouteDefinitionVersion = entity.RouteDefinitionVersion,
		RouteType = entity.RouteType,
		Status = entity.Status,
		RouteData = Deserialize<AdmissionCrmSpecialRouteDataDto>(entity.RouteDataJson) ?? new(),
		EvidenceReferences = Deserialize<IReadOnlyList<string>>(entity.EvidenceJson) ?? [],
		PreviousAssignmentId = entity.PreviousAssignmentId,
		AssignmentReason = entity.AssignmentReason,
		IsPrimary = entity.IsPrimary,
		AssignedAt = entity.AssignedAt,
		EvaluatedAt = entity.EvaluatedAt,
		EvaluationReason = entity.EvaluationReason
	};

	public static AdmissionCrmRouteAppealDto Map(AdmissionCrmRouteAppeal entity) => new()
	{
		Id = entity.Id,
		LeadId = entity.LeadId,
		RouteAssignmentId = entity.RouteAssignmentId,
		OriginalStatus = entity.OriginalStatus,
		RequestedStatus = entity.RequestedStatus,
		Reason = entity.Reason,
		EvidenceReferences = Deserialize<IReadOnlyList<string>>(entity.EvidenceJson) ?? [],
		Status = entity.Status,
		RequestedAt = entity.RequestedAt,
		DecidedAt = entity.DecidedAt,
		DecisionReason = entity.DecisionReason
	};

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}
