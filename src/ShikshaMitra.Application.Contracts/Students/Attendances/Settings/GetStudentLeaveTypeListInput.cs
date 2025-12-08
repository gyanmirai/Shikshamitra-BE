using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendances.Settings;

/// <summary>
/// Input DTO for getting list of student leave types with pagination and filtering
/// </summary>
public class GetStudentLeaveTypeListInput : PagedAndSortedResultRequestDto
{
	/// <summary>
	/// Filter text to search in leave type name
	/// </summary>
	public string? FilterText { get; set; }

	/// <summary>
	/// Filter by active/inactive status
	/// </summary>
	public bool? IsActive { get; set; }

	public GetStudentLeaveTypeListInput()
	{
		Sorting = nameof(StudentLeaveTypeDto.LeaveTypeName);
		MaxResultCount = 100;
	}
}
