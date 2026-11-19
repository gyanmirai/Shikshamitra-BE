using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentDirectoryRequest : PagedAndSortedResultRequestDto
{
	public List<StudentDirectoryColumnFilterDto> ColumnFilters { get; set; } = [];
	[StringLength(150)] public string? Search { get; set; }
	public string Population { get; set; } = "All";
	public Guid? AcademicYearId { get; set; }
	public Guid? StudentId { get; set; }
	public List<Guid> StudentIds { get; set; } = [];
	public bool IncludeActiveStudentList { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? AcademicMediumStageId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public List<Guid> BoardIds { get; set; } = [];
	public List<Guid> MediumIds { get; set; } = [];
	public List<Guid> StandardIds { get; set; } = [];
	public List<Guid> DivisionIds { get; set; } = [];
}

public class StudentDirectoryColumnFilterDto
{
	[Required, StringLength(30)] public string Column { get; set; } = "";
	[Required, StringLength(30)] public string Operator { get; set; } = "";
	[StringLength(150)] public string? Value { get; set; }
	public decimal? Min { get; set; }
	public decimal? Max { get; set; }
}

public class StudentDirectoryItemDto : EntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? GrNumber { get; set; }
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public string? StandardName { get; set; }
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public Guid AcademicMediumId { get; set; }
	public string? AcademicMediumName { get; set; }
	public string EntrySource { get; set; } = "CompletedOnboarding";
	public bool HasPhoto { get; set; }
	public List<string> MaskedFields { get; set; } = [];
}

public class StudentEnrollmentHistoryDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string? AcademicYearName { get; set; }
	public Guid? StandardId { get; set; }
	public string? StandardName { get; set; }
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public int? RollNumber { get; set; }
	public bool IsActive { get; set; }
	public Guid? OnboardingCaseId { get; set; }
}

public class RequestStudentLegacyOverrideDto
{
	[Required, StringLength(2000, MinimumLength = 10)] public string Reason { get; set; } = "";
	[StringLength(1000)] public string? EvidenceReference { get; set; }
}

public class DecideStudentLegacyOverrideDto
{
	[Required, StringLength(40)] public string ConcurrencyStamp { get; set; } = "";
	[StringLength(2000)] public string? Remarks { get; set; }
}

public class StudentLegacyOverrideDto : EntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public Guid RequestedByUserId { get; set; }
	public Guid? DecidedByUserId { get; set; }
	public string Reason { get; set; } = "";
	public string? EvidenceReference { get; set; }
	public string? DecisionRemarks { get; set; }
	public string Status { get; set; } = "Pending";
	public DateTime CreationTime { get; set; }
	public DateTime? DecidedAt { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
}

public interface IStudentDirectoryAppService : IApplicationService
{
	Task<StudentDirectoryOptionsDto> GetOptionsAsync(Guid? academicYearId = null);
	Task<PagedResultDto<StudentDirectoryItemDto>> GetListAsync(StudentDirectoryRequest input);
	Task<PagedResultDto<StudentDirectoryItemDto>> GetLegacyCandidatesAsync(StudentDirectoryRequest input);
	Task<PagedResultDto<StudentLegacyOverrideDto>> GetLegacyOverridesAsync(PagedResultRequestDto input);
	Task<StudentLegacyOverrideDto> RequestLegacyOverrideAsync(Guid studentId, RequestStudentLegacyOverrideDto input);
	Task<StudentLegacyOverrideDto> ApproveLegacyOverrideAsync(Guid id, DecideStudentLegacyOverrideDto input);
	Task<StudentLegacyOverrideDto> RejectLegacyOverrideAsync(Guid id, DecideStudentLegacyOverrideDto input);
}

public class StudentDirectoryOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public Guid? ParentId { get; set; }
	public Guid? StageId { get; set; }
}
public class StudentDirectoryOptionsDto
{
	public List<StudentDirectoryOptionDto> AcademicYears { get; set; } = [];
	public List<StudentDirectoryOptionDto> Boards { get; set; } = [];
	public List<StudentDirectoryOptionDto> Mediums { get; set; } = [];
	public List<StudentDirectoryOptionDto> Stages { get; set; } = [];
	public List<StudentDirectoryOptionDto> Standards { get; set; } = [];
	public List<StudentDirectoryOptionDto> Divisions { get; set; } = [];
}
