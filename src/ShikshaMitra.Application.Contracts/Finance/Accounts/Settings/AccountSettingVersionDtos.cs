using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Accounts.Settings.Dtos;

public class AccountSettingVersionHistoryItemDto : EntityDto<Guid>
{
	public int VersionNumber { get; set; }
	public string ChangeAction { get; set; } = string.Empty;
	public DateTime ChangeTime { get; set; }
	public Guid? ActorId { get; set; }
	public string? ActorName { get; set; }
	public List<AccountSettingVersionFieldChangeDto> ChangedFields { get; set; } = [];
}

public class AccountSettingVersionFieldChangeDto
{
	public string FieldName { get; set; } = string.Empty;
	public string? OriginalValue { get; set; }
	public string? NewValue { get; set; }
}

public class AccountSettingStatusUpdateRequest
{
	public bool IsActive { get; set; }
	public string? DeactivationReason { get; set; }
	public string? ConcurrencyStamp { get; set; }
}
