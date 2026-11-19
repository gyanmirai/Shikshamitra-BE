using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>A private, expiring capability; neither a portal account nor delegated staff identity.</summary>
public class AdmissionParentRegistration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public string TokenHash { get; private set; } = "";
	public string ProtectedToken { get; private set; } = "";
	public string RecipientJson { get; private set; } = "";
	public string RecipientFingerprint { get; private set; } = "";
	public string Purpose { get; private set; } = "initial";
	public string Status { get; private set; } = "Invited";
	public string DeliveryStatus { get; private set; } = "Pending";
	public string? DeliveryError { get; private set; }
	public Guid? CampaignId { get; private set; }
	public bool AllowsDocuments { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	public DateTime? LastSavedAt { get; private set; }
	public DateTime? HandedInAt { get; private set; }
	public DateTime? DeliveryStartedAt { get; private set; }
	public string? Notes { get; private set; }
	public string FieldCodesJson { get; private set; } = "[]";
	public string DocumentsJson { get; private set; } = "[]";
	public string ProposedValuesJson { get; private set; } = "[]";
	public string? ProposalBaseline { get; private set; }
	public string RequestFingerprint { get; private set; } = "";
	public string OperationsJson { get; private set; } = "{}";
	protected AdmissionParentRegistration() { }
	public AdmissionParentRegistration(Guid id, Guid tenantId, Guid applicationId, Guid requestedBy, string tokenHash,
		string protectedToken, string recipientJson, string recipientFingerprint, string purpose, DateTime now,
		string fingerprint, bool allowsDocuments, string fieldCodes = "[]", string documents = "[]", string? notes = null) : base(id)
	{
		if (requestedBy == Guid.Empty || tenantId == Guid.Empty || applicationId == Guid.Empty || tokenHash.Length != 64 || purpose is not ("initial" or "correction"))
			throw new BusinessException(AdmissionParentRegistrationErrors.Invalid);
		TenantId = tenantId; ApplicationId = applicationId; RequestedBy = requestedBy; TokenHash = tokenHash;
		ProtectedToken = protectedToken; RecipientJson = recipientJson; RecipientFingerprint = recipientFingerprint;
		Purpose = purpose; ExpiresAt = now.AddDays(7); RequestFingerprint = fingerprint; AllowsDocuments = allowsDocuments;
		FieldCodesJson = fieldCodes; DocumentsJson = documents; Notes = notes;
	}
	public bool CanRead(DateTime now) => ExpiresAt > now && Status is "Invited" or "InProgress" or "ReadyForReview";
	public bool CanWrite(DateTime now) => CanRead(now) && Status is "Invited" or "InProgress";
	public void Save(DateTime now, string? proposedValues = null, string? baseline = null)
	{
		EnsureWritable(now); Status = "InProgress"; LastSavedAt = now;
		if (proposedValues != null) { ProposedValuesJson = proposedValues; ProposalBaseline = baseline; }
	}
	public void RebaseProposal(string baseline) { if (ProposalBaseline != null) ProposalBaseline = baseline; }
	public void HandIn(DateTime now) { EnsureWritable(now); Status = "ReadyForReview"; HandedInAt = now; }
	public void Close(string status)
	{
		if (status is not ("Revoked" or "TakenOver" or "Applied" or "Closed")) throw new ArgumentException(nameof(status));
		Status = status; ProtectedToken = "";
		if (DeliveryStatus is "Pending" or "PendingApproval") DeliveryStatus = "Cancelled";
	}
	public void SetDelivery(string status, string? error = null, Guid? campaignId = null, DateTime? startedAt = null)
	{
		DeliveryStatus = status; DeliveryError = error; CampaignId = campaignId ?? CampaignId; DeliveryStartedAt = startedAt ?? DeliveryStartedAt;
		if (status == "Sent") ProtectedToken = "";
	}
	public bool IsReplay(string key, string fingerprint)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > 128) throw new BusinessException(AdmissionParentRegistrationErrors.Invalid);
		var operations = JsonSerializer.Deserialize<Dictionary<string, string>>(OperationsJson)!;
		if (!operations.TryGetValue(key, out var previous)) return false;
		if (previous != fingerprint) throw new BusinessException(AdmissionParentRegistrationErrors.Conflict);
		return true;
	}
	public void RecordOperation(string key, string fingerprint)
	{
		if (IsReplay(key, fingerprint)) return;
		var operations = JsonSerializer.Deserialize<Dictionary<string, string>>(OperationsJson)!;
		if (operations.Count >= 1000) throw new BusinessException(AdmissionParentRegistrationErrors.Invalid, "Start a new registration request before making further changes.");
		operations.Add(key, fingerprint); OperationsJson = JsonSerializer.Serialize(operations);
	}
	private void EnsureWritable(DateTime now) { if (!CanWrite(now)) throw new BusinessException(AdmissionParentRegistrationErrors.Unavailable); }
	public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
