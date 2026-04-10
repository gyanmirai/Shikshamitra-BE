using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Attendance.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance.Settings;

/// <summary>
/// Application service for managing student leave types
/// </summary>
[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
public class StudentLeaveTypeAppService(
	IStudentLeaveTypeRepository studentLeaveTypeRepository,
	StudentLeaveTypeManager studentLeaveTypeManager) : ApplicationService, IStudentLeaveTypeAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<PagedResultDto<StudentLeaveTypeDto>> GetListAsync(GetStudentLeaveTypeListInput input)
	{
		var totalCount = await studentLeaveTypeRepository.GetCountAsync(
			input.FilterText,
			input.IsActive,
			CurrentTenant.Id);

		var leaveTypes = await studentLeaveTypeRepository.GetListAsync(
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
		var leaveType = await studentLeaveTypeRepository.GetAsync(id);
		return ObjectMapper.Map<StudentLeaveType, StudentLeaveTypeDto>(leaveType);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	public async Task<StudentLeaveTypeDto> CreateAsync(CreateUpdateStudentLeaveTypeDto input)
	{
		var leaveType = await studentLeaveTypeManager.CreateAsync(
			input.LeaveTypeName,
			input.Color,
			input.IsActive);

		await studentLeaveTypeRepository.InsertAsync(leaveType, autoSave: true);

		return ObjectMapper.Map<StudentLeaveType, StudentLeaveTypeDto>(leaveType);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	public async Task<StudentLeaveTypeDto> UpdateAsync(Guid id, CreateUpdateStudentLeaveTypeDto input)
	{
		var leaveType = await studentLeaveTypeRepository.GetAsync(id);

		await studentLeaveTypeManager.UpdateAsync(
			leaveType,
			input.LeaveTypeName,
			input.Color,
			input.IsActive);

		await studentLeaveTypeRepository.UpdateAsync(leaveType, autoSave: true);

		return ObjectMapper.Map<StudentLeaveType, StudentLeaveTypeDto>(leaveType);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var leaveType = await studentLeaveTypeRepository.GetAsync(id);

		await studentLeaveTypeManager.ValidateDeleteAsync(leaveType);

		await studentLeaveTypeRepository.DeleteAsync(leaveType, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<ListResultDto<StudentLeaveTypeDto>> GetActiveListAsync()
	{
		var leaveTypes = await studentLeaveTypeRepository.GetActiveLeaveTypesAsync(CurrentTenant.Id);

		return new ListResultDto<StudentLeaveTypeDto>(
			ObjectMapper.Map<List<StudentLeaveType>, List<StudentLeaveTypeDto>>(leaveTypes));
	}
}
