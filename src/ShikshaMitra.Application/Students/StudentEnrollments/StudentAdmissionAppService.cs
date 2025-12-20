using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for Student Admission operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentAdmissionAppService : ApplicationService, IStudentAdmissionAppService
{
	private readonly IRepository<Student, Guid> studentRepository;
	private readonly IRepository<Division, Guid> divisionRepository;

	public StudentAdmissionAppService(
		IRepository<Student, Guid> studentRepository,
		IRepository<Division, Guid> divisionRepository)
	{
		this.studentRepository = studentRepository;
		this.divisionRepository = divisionRepository;
	}

	/// <summary>
	/// Gets a paginated list of students in admission stage with filters
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.View)]
	public async Task<PagedResultDto<StudentEnrollmentListDto>> GetListAsync(GetStudentAdmissionsInput input)
	{
		var query = (await studentRepository.GetQueryableAsync())
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission);

		// Apply filters
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			query = query.Where(s =>
				(s.FirstName != null && s.FirstName.Contains(input.Search)) ||
				(s.MiddleName != null && s.MiddleName.Contains(input.Search)) ||
				(s.LastName != null && s.LastName.Contains(input.Search)) ||
				(s.GrNo != null && s.GrNo.ToString().Contains(input.Search)));
		}

		query = query.Where(s => s.AdmissionApprovalStatus == input.ApprovalStatus);

		// Get total count
		var totalCount = await studentRepository.AsyncExecuter.CountAsync(query);

		// Apply sorting and paging
		query = query.OrderByDescending(s => s.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		// Execute query and project to DTO
		var students = await studentRepository.AsyncExecuter.ToListAsync(
			query
		);

		var studentDtos = ObjectMapper.Map<List<Student>, List<StudentEnrollmentListDto>>(students);

		return new PagedResultDto<StudentEnrollmentListDto>(totalCount, studentDtos);
	}

	/// <summary>
	/// Assigns or changes division for a student
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task AssignDivisionAsync(Guid studentId, AssignDivisionDto input)
	{
		// Validate division exists
		var divisionExists = await divisionRepository.AnyAsync(d => d.Id == input.DivisionId);
		if (!divisionExists)
		{
			throw new BusinessException("StudentAdmission:DivisionNotFound")
				.WithData("DivisionId", input.DivisionId);
		}

		// Get student
		var student = await studentRepository.GetAsync(studentId);

		// Assign division using domain method
		student.AssignDivision(input.DivisionId);

		// Save changes
		await studentRepository.UpdateAsync(student);
	}

	/// <summary>
	/// Changes admission status (approve/reject) for multiple students
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task<BatchAdmissionStatusChangeResultDto> ChangeStatusBatchAsync(BatchAdmissionStatusChangeRequestDto input)
	{
		var result = new BatchAdmissionStatusChangeResultDto();

		foreach (var item in input.Items)
		{
			var itemResult = new AdmissionStatusChangeItemResultDto
			{
				StudentId = item.StudentId
			};

			try
			{
				// Get student
				var student = await studentRepository.GetAsync(item.StudentId);

				// Apply action
				if (item.Action == AdmissionAction.Approve)
				{
					student.ApproveAdmission();
					itemResult.NewStatus = StudentEnrollmentApprovalState.Approved.ToString();
					itemResult.Message = "Admission approved successfully";
				}
				else if (item.Action == AdmissionAction.Reject)
				{
					if (string.IsNullOrWhiteSpace(item.Remarks))
					{
						throw new BusinessException("StudentAdmission:RemarksRequired")
							.WithData("StudentId", item.StudentId);
					}

					student.RejectAdmission(item.Remarks);
					itemResult.NewStatus = StudentEnrollmentApprovalState.Rejected.ToString();
					itemResult.Message = "Admission rejected successfully";
				}
				else
				{
					// Explicitly handle invalid/unsupported actions
					throw new BusinessException("StudentAdmission:InvalidAction")
						.WithData("StudentId", item.StudentId)
						.WithData("Action", item.Action);
				}

				// Save changes
				await studentRepository.UpdateAsync(student);

				itemResult.Success = true;
				result.Succeeded++;
			}
			catch (BusinessException ex)
			{
				// BusinessExceptions are user-facing and safe to expose
				itemResult.Success = false;
				itemResult.Message = ex.Message;
				result.Failed++;
				
				// Log the business exception for diagnostics
				Logger.LogWarning(ex, "Business exception occurred while processing admission status change for student {StudentId}", item.StudentId);
			}
			catch (Exception ex)
			{
				// Unexpected exceptions get a generic message to avoid leaking internal details
				itemResult.Success = false;
				itemResult.Message = "An unexpected error occurred while processing admission status.";
				result.Failed++;
				
				// Log the full exception for diagnostics
				Logger.LogError(ex, "Unexpected error occurred while processing admission status change for student {StudentId}", item.StudentId);
			}

			result.Results.Add(itemResult);
		}

		return result;
	}
}
