using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationOptOut : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? LeadId { get; private set; }
	public AdmissionCrmMessageChannel Channel { get; private set; }
	public string ContactValue { get; private set; } = string.Empty;
	public string NormalizedContactValue { get; private set; } = string.Empty;
	public bool IsActive { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public DateTime OptedOutAt { get; private set; }
	public DateTime? OptedInAt { get; private set; }
	public string? OptInReason { get; private set; }

	protected AdmissionCrmCommunicationOptOut()
	{
	}

	public AdmissionCrmCommunicationOptOut(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? leadId,
		AdmissionCrmMessageChannel channel,
		string contactValue,
		string reason,
		DateTime optedOutAt)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		Channel = channel;
		ContactValue = Check.NotNullOrWhiteSpace(
			contactValue,
			nameof(contactValue),
			AdmissionCrmConsts.MaxEmailLength);
		NormalizedContactValue = NormalizeContact(channel, ContactValue);
		Reason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			AdmissionCrmConsts.MaxNotesLength);
		OptedOutAt = optedOutAt;
		IsActive = true;
	}

	public void Reactivate(Guid? leadId, string reason, DateTime optedOutAt)
	{
		LeadId = leadId ?? LeadId;
		Reason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			AdmissionCrmConsts.MaxNotesLength);
		OptedOutAt = optedOutAt;
		OptedInAt = null;
		OptInReason = null;
		IsActive = true;
	}

	public void Clear(string reason, DateTime optedInAt)
	{
		OptInReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			AdmissionCrmConsts.MaxNotesLength);
		OptedInAt = optedInAt;
		IsActive = false;
	}

	public static string NormalizeContact(AdmissionCrmMessageChannel channel, string value)
	{
		var trimmed = Check.NotNullOrWhiteSpace(
			value,
			nameof(value),
			AdmissionCrmConsts.MaxEmailLength)
			.Trim();

		return channel == AdmissionCrmMessageChannel.Email
			? trimmed.ToLowerInvariant()
			: NormalizePhone(trimmed);
	}

	private static string NormalizePhone(string value)
	{
		var digits = new string(value.Where(char.IsDigit).ToArray());
		if (string.IsNullOrWhiteSpace(digits))
		{
			throw new BusinessException("AdmissionCrm:CommunicationContactInvalid");
		}

		return digits;
	}
}
