using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ShikshaMitra.Models;

/// <summary>
/// Multipart/form-data request for creating employee personal info with an optional profile picture.
/// The <see cref="Data"/> field must contain a JSON string matching the <c>PersonalInfoRequest</c> schema.
/// </summary>
public class CreatePersonalInfoWithPictureRequest
{
	/// <summary>
	/// JSON string matching the PersonalInfoRequest schema
	/// </summary>
	[Required]
	public string Data { get; set; } = null!;

	/// <summary>
	/// Optional profile picture file (jpg/png, max 3 MB — validated by the domain layer)
	/// </summary>
	public IFormFile? ProfilePicture { get; set; }
}
