using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Models;

public class EmployeeAttendanceImportRequest
{
	/// <summary>
	/// The employee attendance file
	/// </summary>
	[Required]
	[FromForm(Name = "file")]
	public IFormFile File { get; set; } = null!;

	[Required]
	[FromForm(Name = "date")]
	public DateTimeOffset Date { get; set; }
}
