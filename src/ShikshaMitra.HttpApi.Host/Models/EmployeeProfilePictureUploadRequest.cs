using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ShikshaMitra.Models;

/// <summary>
/// Request model for uploading employee profile picture via multipart/form-data
/// </summary>
public class EmployeeProfilePictureUploadRequest
{
	/// <summary>
	/// The profile picture file to upload
	/// </summary>
	[Required]
	public IFormFile File { get; set; } = null!;
}
