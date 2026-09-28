using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionIntakeProfileDto : EntityDto<Guid>
{
	public AdmissionIntakeProfileKind Kind { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string ConfigurationJson { get; set; } = "{}";
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class SaveAdmissionIntakeProfileDto
{
	public AdmissionIntakeProfileKind Kind { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string ConfigurationJson { get; set; } = "{}";
	public string? ConcurrencyStamp { get; set; }
}
