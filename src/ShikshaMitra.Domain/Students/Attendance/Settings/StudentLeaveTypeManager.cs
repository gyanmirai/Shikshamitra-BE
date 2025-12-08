using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.Attendance.Settings;

/// <summary>
/// Domain manager for StudentLeaveType aggregate
/// Handles business logic and validation rules
/// </summary>
public class StudentLeaveTypeManager : DomainService
{
	private readonly IStudentLeaveTypeRepository _studentLeaveTypeRepository;

	public StudentLeaveTypeManager(IStudentLeaveTypeRepository studentLeaveTypeRepository)
	{
		_studentLeaveTypeRepository = studentLeaveTypeRepository;
	}

	/// <summary>
	/// Creates a new student leave type with validation
	/// </summary>
	public async Task<StudentLeaveType> CreateAsync(
		string leaveTypeName,
		string color,
		bool isActive = true)
	{
		Check.NotNullOrWhiteSpace(leaveTypeName, nameof(leaveTypeName));
		Check.NotNullOrWhiteSpace(color, nameof(color));

		// Check if leave type name already exists
		await CheckLeaveTypeNameUniquenessAsync(leaveTypeName);

		return new StudentLeaveType(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			leaveTypeName,
			color,
			isActive);
	}

	/// <summary>
	/// Updates an existing student leave type
	/// </summary>
	public async Task UpdateAsync(
		StudentLeaveType studentLeaveType,
		string leaveTypeName,
		string color,
		bool isActive)
	{
		Check.NotNull(studentLeaveType, nameof(studentLeaveType));
		Check.NotNullOrWhiteSpace(leaveTypeName, nameof(leaveTypeName));
		Check.NotNullOrWhiteSpace(color, nameof(color));

		// Check if the new name conflicts with existing leave types
		if (studentLeaveType.LeaveTypeName != leaveTypeName)
		{
			await CheckLeaveTypeNameUniquenessAsync(leaveTypeName, studentLeaveType.Id);
		}

		studentLeaveType.SetLeaveTypeName(leaveTypeName);
		studentLeaveType.SetColor(color);

		if (isActive)
		{
			studentLeaveType.Activate();
		}
		else
		{
			studentLeaveType.Deactivate();
		}
	}

	/// <summary>
	/// Checks if a leave type name is unique within the tenant
	/// </summary>
	private async Task CheckLeaveTypeNameUniquenessAsync(string leaveTypeName, Guid? excludeId = null)
	{
		var exists = await _studentLeaveTypeRepository.IsNameExistsAsync(
			leaveTypeName,
			excludeId,
			CurrentTenant.Id);

		if (exists)
		{
			throw new BusinessException("SM:StudentLeaveTypeNameAlreadyExists")
				.WithData("LeaveTypeName", leaveTypeName);
		}
	}

	/// <summary>
	/// Validates if a leave type can be deleted
	/// </summary>
	public async Task ValidateDeleteAsync(StudentLeaveType studentLeaveType)
	{
		Check.NotNull(studentLeaveType, nameof(studentLeaveType));

		// Additional business rules can be added here
		// For example, check if there are any attendance entries using this leave type
		// If needed, we can add a check for related attendance entries in the future
		
		await Task.CompletedTask;
	}
}
