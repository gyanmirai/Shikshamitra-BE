using System;
using System.Text.Json.Serialization;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Common.IdentityCards;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class IdentityCardPhotoDto
{
	public Guid RecordId { get; set; }
	public string SourceHash { get; set; } = string.Empty;
	[DisableAuditing] public string ImageData { get; set; } = string.Empty;
}
