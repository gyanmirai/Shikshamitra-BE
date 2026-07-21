using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class StudentFeePolicyOverrideDto : EntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public Guid FeeStructureId { get; set; }
	public bool IsLateFeeExempt { get; set; }
	public int? CustomGracePeriodDays { get; set; }
	public bool WaiverPreApproved { get; set; }
	public string? WaiverPreApprovalReason { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateStudentFeePolicyOverrideDto
{
	public bool IsLateFeeExempt { get; set; }
	public int? CustomGracePeriodDays { get; set; }
	public bool WaiverPreApproved { get; set; }
	public string? WaiverPreApprovalReason { get; set; }
}

/// <summary>
/// The resolved outcome of CollectionPolicyResolver.ResolveEffectiveRulesForStudentAsync,
/// returned alongside the raw override so the frontend override-editing UI can show staff
/// the combined result, not just what they just typed.
/// </summary>
public class EffectiveCollectionRulesDto
{
	public CollectionPolicyDto? Policy { get; set; }
	public bool IsLateFeeExempt { get; set; }
	public int? CustomGracePeriodDays { get; set; }
	public bool WaiverPreApproved { get; set; }
}
