using System.ComponentModel.DataAnnotations;
using Volo.Abp.Auditing;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomImageInput
{
	[Required, StringLength(255)]
	public string FileName { get; set; } = string.Empty;
	[Required, MaxLength(5 * 1024 * 1024), DisableAuditing]
	public byte[] Content { get; set; } = [];
}

public class RoomImageDto
{
	public string ContentType { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
}
