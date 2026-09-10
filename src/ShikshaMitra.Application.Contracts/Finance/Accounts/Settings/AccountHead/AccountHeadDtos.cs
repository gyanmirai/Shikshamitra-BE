using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class AccountHeadUpsertRequest
{
	public string HeadName { get; set; } = string.Empty;
	public AccountHeadType HeadType { get; set; }
	public Guid? ParentAccountHeadId { get; set; }
	public string? Notes { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class AccountHeadResponse : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string HeadName { get; set; } = string.Empty;
	public AccountHeadType HeadType { get; set; }
	public string Nature { get; set; } = string.Empty;
	public Guid? ParentAccountHeadId { get; set; }
	public string? ParentAccountHeadName { get; set; }
	public string? Notes { get; set; }
	public bool IsActive { get; set; }
	public string? DeactivationReason { get; set; }
	public int Version { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool IsUsedInDownstream { get; set; }
	public bool CanDelete => !IsActive && !IsUsedInDownstream;
}

public class AccountHeadOptionDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public AccountHeadType HeadType { get; set; }
	public string DisplayName => Name;
}
