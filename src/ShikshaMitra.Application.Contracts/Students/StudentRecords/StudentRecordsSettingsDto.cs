using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public sealed class StudentRecordsSettingsDto
{
	public Guid? Id { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public StudentRecordsPolicy DraftPolicy { get; set; } = StudentRecordsPolicy.Starter();
	public StudentRecordsPolicy EffectivePolicy { get; set; } = StudentRecordsPolicy.Starter();
	public Guid? PublishedVersionId { get; set; }
	public int PublishedVersionNumber { get; set; }
	public bool IsStarter { get; set; }
	public Guid? EffectiveScopeAcademicYearId { get; set; }
}

public sealed class SaveStudentRecordsSettingsDto
{
	public Guid? AcademicYearId { get; set; }
	[StringLength(40)] public string? ConcurrencyStamp { get; set; }
	[Required] public StudentRecordsPolicy Policy { get; set; } = new();
}

public sealed class PublishStudentRecordsSettingsDto
{
	public Guid? AcademicYearId { get; set; }
	[Required, StringLength(40)] public string ConcurrencyStamp { get; set; } = "";
}

public sealed class StudentRecordsSettingsPreviewDto
{
	public StudentRecordsPolicy Policy { get; set; } = new();
	public List<string> Summary { get; set; } = [];
}

public sealed class StudentRecordsRoleNamesDto
{
	public List<StudentRecordsRoleNameDto> Items { get; set; } = [];
}

public sealed class StudentRecordsRoleNameDto
{
	public string Name { get; set; } = "";
}

public interface IStudentRecordsSettingsAppService : IApplicationService
{
	Task<StudentRecordsSettingsDto> GetAsync(Guid? academicYearId);
	Task<StudentRecordsSettingsDto> SaveDraftAsync(SaveStudentRecordsSettingsDto input);
	Task<StudentRecordsSettingsPreviewDto> PreviewAsync(SaveStudentRecordsSettingsDto input);
	Task<StudentRecordsSettingsDto> PublishAsync(PublishStudentRecordsSettingsDto input);
	Task<StudentRecordsRoleNamesDto> GetRoleNamesAsync();
}
