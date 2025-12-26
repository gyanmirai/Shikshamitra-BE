using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Models;

public class EmployeeAttendanceImportRequest
{
	/// <summary>
	/// The employee attendance file
	/// </summary>
	[Required]
	public IFormFile File { get; set; } = null!;

	[Required]
	public DateTimeOffset Date { get; set; }
}
