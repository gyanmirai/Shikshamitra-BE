using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance;

public sealed class AttendanceSettingsDto
{
	public Guid? Id { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public AttendancePolicy DraftPolicy { get; set; } = new();
	public AttendancePolicy EffectivePolicy { get; set; } = new();
	public Guid? PublishedVersionId { get; set; }
	public int PublishedVersionNumber { get; set; }
	public bool IsStarter { get; set; }
	public Guid? EffectiveScopeAcademicYearId { get; set; }
	public List<AttendancePolicyVersionDto> Versions { get; set; } = [];
}
public sealed class AttendancePolicyVersionDto
{
	public Guid Id { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int VersionNumber { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime PublishedAt { get; set; }
}
public sealed class SaveAttendanceSettingsDto
{
	public Guid? AcademicYearId { get; set; }
	public string? ConcurrencyStamp { get; set; }
	[Required] public AttendancePolicy Policy { get; set; } = new();
}
public sealed class PublishAttendanceSettingsDto
{
	public Guid? AcademicYearId { get; set; }
	[Required] public string ConcurrencyStamp { get; set; } = "";
	public DateTime EffectiveFrom { get; set; }
}
public sealed class AttendanceSettingsPreviewDto
{
	public AttendancePolicy Policy { get; set; } = new();
	public List<string> Summary { get; set; } = [];
	public List<AttendancePolicyExampleDto> Examples { get; set; } = [];
}
public sealed class AttendancePolicyExampleDto
{
	public string Label { get; set; } = "";
	public string Outcome { get; set; } = "";
}
public interface IAttendanceSettingsAppService : IApplicationService
{
	Task<AttendanceSettingsDto> GetAsync(Guid? academicYearId);
	Task<AttendanceSettingsDto> SaveDraftAsync(SaveAttendanceSettingsDto input);
	Task<AttendanceSettingsPreviewDto> PreviewAsync(SaveAttendanceSettingsDto input);
	Task<AttendanceSettingsDto> PublishAsync(PublishAttendanceSettingsDto input);
}
