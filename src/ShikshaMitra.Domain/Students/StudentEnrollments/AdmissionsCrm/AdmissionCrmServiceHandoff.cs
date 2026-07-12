using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmServiceHandoff : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid ActivationId { get; private set; }
	public Guid StudentId { get; private set; }
	public AdmissionCrmServiceHandoffType Type { get; private set; }
	public string DestinationModule { get; private set; } = string.Empty;
	public bool ConsentConfirmed { get; private set; }
	public string PayloadJson { get; private set; } = "{}";
	public AdmissionCrmServiceHandoffStatus Status { get; private set; }
	public int AttemptCount { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public DateTime? DispatchedAt { get; private set; }
	public DateTime? AcknowledgedAt { get; private set; }
	public string? ExternalReference { get; private set; }
	public string? LastError { get; private set; }

	protected AdmissionCrmServiceHandoff() { }

	public AdmissionCrmServiceHandoff(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid activationId, Guid studentId, AdmissionCrmServiceHandoffType type, string destinationModule,
		bool consentConfirmed, string payloadJson, DateTime requestedAt) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId;
		ActivationId = activationId; StudentId = studentId; Type = type;
		DestinationModule = Check.NotNullOrWhiteSpace(destinationModule, nameof(destinationModule), AdmissionCrmConsts.MaxNameLength);
		ConsentConfirmed = consentConfirmed; PayloadJson = ValidatePayload(payloadJson);
		RequestedAt = requestedAt.ToUniversalTime(); Status = consentConfirmed
			? AdmissionCrmServiceHandoffStatus.Pending : AdmissionCrmServiceHandoffStatus.Failed;
		if (!consentConfirmed) LastError = "Required service-handoff consent was not confirmed.";
	}

	public void MarkDispatched(DateTime at, string? externalReference)
	{
		if (!ConsentConfirmed) throw new BusinessException("AdmissionCrm:ServiceHandoffConsentRequired");
		if (Status is AdmissionCrmServiceHandoffStatus.Acknowledged or AdmissionCrmServiceHandoffStatus.Cancelled)
			throw new BusinessException("AdmissionCrm:ServiceHandoffClosed");
		AttemptCount++; DispatchedAt = at.ToUniversalTime(); ExternalReference = Normalize(externalReference);
		LastError = null; Status = AdmissionCrmServiceHandoffStatus.Dispatched;
	}

	public void Acknowledge(DateTime at, string externalReference)
	{
		if (Status != AdmissionCrmServiceHandoffStatus.Dispatched)
			throw new BusinessException("AdmissionCrm:ServiceHandoffNotDispatched");
		ExternalReference = Check.NotNullOrWhiteSpace(externalReference, nameof(externalReference), AdmissionCrmConsts.MaxNameLength);
		AcknowledgedAt = at.ToUniversalTime(); Status = AdmissionCrmServiceHandoffStatus.Acknowledged;
	}

	public void Fail(string error)
	{
		if (Status is AdmissionCrmServiceHandoffStatus.Acknowledged or AdmissionCrmServiceHandoffStatus.Cancelled)
			throw new BusinessException("AdmissionCrm:ServiceHandoffClosed");
		LastError = Check.NotNullOrWhiteSpace(error, nameof(error), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmServiceHandoffStatus.Failed;
	}

	public void Cancel(string reason)
	{
		if (Status == AdmissionCrmServiceHandoffStatus.Acknowledged)
			throw new BusinessException("AdmissionCrm:ServiceHandoffAlreadyAcknowledged");
		LastError = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmServiceHandoffStatus.Cancelled;
	}

	private static string ValidatePayload(string json)
	{
		var value = Check.NotNullOrWhiteSpace(json, nameof(json), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new BusinessException("AdmissionCrm:InvalidServiceHandoffPayload");
		return value;
	}

	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value)
		? null : Check.Length(value.Trim(), nameof(value), AdmissionCrmConsts.MaxNameLength);
}
