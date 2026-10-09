using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HostTenants;

public class HostTenantDetailsInput
{
	[Required, StringLength(HostTenantConsts.MaxInstituteNameLength)]
	public string InstituteName { get; set; } = string.Empty;
	[Required, EmailAddress, StringLength(256)]
	public string Email { get; set; } = string.Empty;
	[Required, StringLength(HostTenantConsts.MaxContactLength)]
	public string ContactNumber { get; set; } = string.Empty;
	[Required, StringLength(HostTenantConsts.MaxAddressLength)]
	public string Address1 { get; set; } = string.Empty;
	[StringLength(HostTenantConsts.MaxAddressLength)]
	public string? Address2 { get; set; }
	[StringLength(HostTenantConsts.MaxAddressLength)]
	public string? Address3 { get; set; }
	[Required, StringLength(128)]
	public string City { get; set; } = string.Empty;
	[Required, StringLength(128)]
	public string State { get; set; } = string.Empty;
	[Required, StringLength(128)]
	public string Country { get; set; } = string.Empty;
	[Required, StringLength(20)]
	public string PostalCode { get; set; } = string.Empty;
}

public class CreateHostTenantInput : HostTenantDetailsInput
{
	[Required, StringLength(HostTenantConsts.MaxCodeLength), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9_-]*$")]
	public string Name { get; set; } = string.Empty;
	[Required, EmailAddress, StringLength(256)]
	public string AdminEmailAddress { get; set; } = string.Empty;
	[Required, StringLength(128, MinimumLength = 8)]
	public string AdminPassword { get; set; } = string.Empty;
}

public class UpdateHostTenantInput : HostTenantDetailsInput
{
	[Required]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class ChangeHostTenantStatusInput
{
	public bool IsActive { get; set; }
	[StringLength(HostTenantConsts.MaxReasonLength)]
	public string? Reason { get; set; }
	[Required]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class GetHostTenantsInput : PagedAndSortedResultRequestDto
{
	[StringLength(256)]
	public string? Filter { get; set; }
	public bool? IsActive { get; set; }
}

public class HostTenantDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string InstituteName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string ContactNumber { get; set; } = string.Empty;
	public string Address1 { get; set; } = string.Empty;
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public string City { get; set; } = string.Empty;
	public string State { get; set; } = string.Empty;
	public string Country { get; set; } = string.Empty;
	public string PostalCode { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public DateTime CreationTime { get; set; }
}

public class HostSessionContextDto
{
	public bool SetupCompleted { get; set; } = true;
	public bool CanManageSetup { get; set; }
	public string AccountMode { get; set; } = string.Empty;
	public Guid? TenantId { get; set; }
	public bool CanViewTenants { get; set; }
	public bool CanCreateTenants { get; set; }
	public bool CanUpdateTenants { get; set; }
	public bool CanManageTenantStatus { get; set; }
}
