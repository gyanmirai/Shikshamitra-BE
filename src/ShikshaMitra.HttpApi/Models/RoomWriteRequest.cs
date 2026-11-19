using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Auditing;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class RoomWriteRequest
{
	[Required, StringLength(16384)]
	public string Input { get; set; } = string.Empty;

	[DisableValidation, DisableAuditing]
	public IFormFile? Image { get; set; }
}
