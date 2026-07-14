using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmInquirySource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public long AllowedChannelMask { get; private set; }
	public int DisplayOrder { get; private set; }
	public bool IsActive { get; private set; }
	public bool IsRoutingEligible { get; private set; }
	public string? DeactivationReason { get; private set; }

	protected AdmissionCrmInquirySource()
	{
	}

	public AdmissionCrmInquirySource(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string code,
		string displayName,
		string? description,
		IReadOnlyCollection<AdmissionInquiryChannel> allowedChannels,
		int displayOrder,
		bool isRoutingEligible)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Update(code, displayName, description, allowedChannels, displayOrder, isRoutingEligible);
		IsActive = true;
	}

	public void Update(
		string code,
		string displayName,
		string? description,
		IReadOnlyCollection<AdmissionInquiryChannel> allowedChannels,
		int displayOrder,
		bool isRoutingEligible)
	{
		Code = NormalizeCode(code);
		DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), AdmissionCrmConsts.MaxSourceSnapshotLength);
		Description = NormalizeOptional(description, AdmissionCrmConsts.MaxDescriptionLength);
		AllowedChannelMask = BuildChannelMask(allowedChannels);
		if (displayOrder < 0) throw new BusinessException("AdmissionCrm:InvalidInquirySourceDisplayOrder");
		DisplayOrder = displayOrder;
		IsRoutingEligible = isRoutingEligible;
	}

	public void SetActive(bool active, string? reason)
	{
		if (!active && string.IsNullOrWhiteSpace(reason))
			throw new BusinessException("AdmissionCrm:InquirySourceDeactivationReasonRequired");
		IsActive = active;
		DeactivationReason = active ? null : NormalizeOptional(reason, AdmissionCrmConsts.MaxNotesLength);
	}

	public bool Supports(AdmissionInquiryChannel channel) =>
		(AllowedChannelMask & (1L << (int)channel)) != 0;

	public IReadOnlyList<AdmissionInquiryChannel> GetAllowedChannels() =>
		Enum.GetValues<AdmissionInquiryChannel>().Where(Supports).ToList();

	private static string NormalizeCode(string value)
	{
		var code = Check.NotNullOrWhiteSpace(value, nameof(value), AdmissionCrmConsts.MaxPlaybookCodeLength)
			.Trim().ToUpperInvariant();
		if (code.Any(x => !char.IsLetterOrDigit(x) && x is not '-' and not '_'))
			throw new BusinessException("AdmissionCrm:InvalidInquirySourceCode");
		return code;
	}

	private static long BuildChannelMask(IReadOnlyCollection<AdmissionInquiryChannel> channels)
	{
		if (channels.Count == 0) throw new BusinessException("AdmissionCrm:InquirySourceChannelRequired");
		return channels.Distinct().Aggregate(0L, (mask, channel) => mask | (1L << (int)channel));
	}

	private static string? NormalizeOptional(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), maxLength);
}
