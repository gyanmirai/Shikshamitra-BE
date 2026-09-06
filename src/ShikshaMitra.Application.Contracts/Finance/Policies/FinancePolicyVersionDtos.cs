using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class FinancePolicyVersionDto : EntityDto<Guid>
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public int VersionNumber { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string SnapshotJson { get; set; } = "{}";
	public string ChangeAction { get; set; } = "changed";
	public string? ChangeReason { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatorUserName { get; set; }
}

public class FinancePolicyVersionListRequestDto : PagedAndSortedResultRequestDto
{
}
