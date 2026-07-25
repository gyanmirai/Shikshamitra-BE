using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteDefinition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public AdmissionCrmRouteType RouteType { get; private set; }
	public int Version { get; private set; }
	public AdmissionCrmRouteDefinitionStatus Status { get; private set; }
	public string ConfigurationJson { get; private set; } = "{}";
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public Guid? SubmittedByUserId { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }

	protected AdmissionCrmRouteDefinition() { }

	public AdmissionCrmRouteDefinition(Guid id, Guid? tenantId, Guid academicYearId, string code,
		string name, AdmissionCrmRouteType routeType, int version, string configurationJson,
		DateTime effectiveFrom, DateTime? effectiveUntil) : base(id)
	{
		if (version <= 0) throw new BusinessException("AdmissionCrm:InvalidRouteDefinitionVersion");
		TenantId = tenantId; AcademicYearId = academicYearId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPolicyCodeLength).ToUpperInvariant();
		Version = version; Status = AdmissionCrmRouteDefinitionStatus.Draft;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		RouteType = routeType;
		using var document = JsonDocument.Parse(Check.NotNullOrWhiteSpace(configurationJson, nameof(configurationJson), AdmissionCrmConsts.MaxRouteDataJsonLength));
		if (document.RootElement.ValueKind != JsonValueKind.Object) throw new BusinessException("AdmissionCrm:InvalidRouteConfiguration");
		ConfigurationJson = configurationJson; EffectiveFrom = effectiveFrom.ToUniversalTime();
		EffectiveUntil = effectiveUntil?.ToUniversalTime();
		if (EffectiveUntil.HasValue && EffectiveUntil <= EffectiveFrom) throw new BusinessException("AdmissionCrm:InvalidRouteDefinitionPeriod");
	}

	public void Submit(Guid userId)
	{
		if (Status != AdmissionCrmRouteDefinitionStatus.Draft) throw new BusinessException("AdmissionCrm:RouteDefinitionNotDraft");
		SubmittedByUserId = userId; Status = AdmissionCrmRouteDefinitionStatus.ApprovalPending;
	}

	public void Publish(Guid userId)
	{
		if (Status != AdmissionCrmRouteDefinitionStatus.ApprovalPending) throw new BusinessException("AdmissionCrm:RouteDefinitionNotAwaitingApproval");
		if (SubmittedByUserId == userId) throw new BusinessException("AdmissionCrm:DistinctRouteApproverRequired");
		ApprovedByUserId = userId; Status = AdmissionCrmRouteDefinitionStatus.Published;
	}

	public void Retire() => Status = AdmissionCrmRouteDefinitionStatus.Retired;

	public bool IsEffectiveAt(DateTime at)
	{
		var utc = at.ToUniversalTime();
		return Status == AdmissionCrmRouteDefinitionStatus.Published &&
		       EffectiveFrom <= utc &&
		       (!EffectiveUntil.HasValue || utc < EffectiveUntil.Value);
	}
}
