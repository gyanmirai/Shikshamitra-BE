using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Attendance.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendances.Settings;

/// <summary>
/// Application service for managing student leave types
/// </summary>
[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
public class StudentLeaveTypeAppService : ApplicationService, IStudentLeaveTypeAppService
{
	private readonly IStudentLeaveTypeRepository _studentLeaveTypeRepository;
	private readonly StudentLeaveTypeManager _studentLeaveTypeManager;

	public StudentLeaveTypeAppService(
		IStudentLeaveTypeRepository studentLeaveTypeRepository,
		StudentLeaveTypeManager studentLeaveTypeManager)
	{
		_studentLeaveTypeRepository = studentLeaveTypeRepository;
		_studentLeaveTypeManager = studentLeaveTypeManager;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<PagedResultDto<StudentLeaveTypeDto>> GetListAsync(GetStudentLeaveTypeListInput input)
	{
		var totalCount = await _studentLeaveTypeRepository.GetCountAsync(
			input.FilterText,
			input.IsActive,
			CurrentTenant.Id);

		var leaveTypes = await _studentLeaveTypeRepository.GetListAsync(
			input.FilterText,
			input.IsActive,
			input.Sorting,
			input.MaxResultCount,
			input.SkipCount,
			CurrentTenant.Id);

		return new PagedResultDto<StudentLeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<StudentLeaveType>, List<StudentLeaveTypeDto>>(leaveTypes));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<StudentLeaveTypeDto> GetAsync(Guid id)
	{
		var leaveType = await _studentLeaveTypeRepository.GetAsync(id);
		return ObjectMapper.Map<StudentLeaveType, StudentLeaveTypeDto>(leaveType);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	public async Task<StudentLeaveTypeDto> CreateAsync(CreateUpdateStudentLeaveTypeDto input)
	{
		var leaveType = await _studentLeaveTypeManager.CreateAsync(
			input.LeaveTypeName,
			input.Color,
			input.IsActive);

		await _studentLeaveTypeRepository.InsertAsync(leaveType, autoSave: true);

		return ObjectMapper.Map<StudentLeaveType, StudentLeaveTypeDto>(leaveType);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	public async Task<StudentLeaveTypeDto> UpdateAsync(Guid id, CreateUpdateStudentLeaveTypeDto input)
	{
		var leaveType = await _studentLeaveTypeRepository.GetAsync(id);

		await _studentLeaveTypeManager.UpdateAsync(
			leaveType,
			input.LeaveTypeName,
			input.Color,
			input.IsActive);

		await _studentLeaveTypeRepository.UpdateAsync(leaveType, autoSave: true);

		return ObjectMapper.Map<StudentLeaveType, StudentLeaveTypeDto>(leaveType);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var leaveType = await _studentLeaveTypeRepository.GetAsync(id);

		await _studentLeaveTypeManager.ValidateDeleteAsync(leaveType);

		await _studentLeaveTypeRepository.DeleteAsync(leaveType, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<ListResultDto<StudentLeaveTypeDto>> GetActiveListAsync()
	{
		var leaveTypes = await _studentLeaveTypeRepository.GetActiveLeaveTypesAsync(CurrentTenant.Id);

		return new ListResultDto<StudentLeaveTypeDto>(
			ObjectMapper.Map<List<StudentLeaveType>, List<StudentLeaveTypeDto>>(leaveTypes));
	}
}
