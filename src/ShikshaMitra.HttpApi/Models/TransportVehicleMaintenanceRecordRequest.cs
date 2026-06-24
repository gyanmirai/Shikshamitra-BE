using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class TransportVehicleMaintenanceRecordRequest
{
	[Required]
	public string ServiceType { get; set; } = string.Empty;

	[Required]
	public DateTime ServiceDate { get; set; }

	public DateTime? NextServiceDate { get; set; }
	public decimal? Cost { get; set; }
	public string? Provider { get; set; }
	public string? Notes { get; set; }

	[DisableValidation]
	public List<IFormFile> Photos { get; set; } = new();
}
