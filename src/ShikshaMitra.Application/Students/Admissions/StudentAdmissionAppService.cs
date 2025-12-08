using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Admissions;

/// <summary>
/// Application service for Student Admission operations
/// </summary>
public class StudentAdmissionAppService : ApplicationService, IStudentAdmissionAppService
{
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;

	public StudentAdmissionAppService(
		IRepository<Student, Guid> studentRepository,
		IRepository<Division, Guid> divisionRepository)
	{
		_studentRepository = studentRepository;
		_divisionRepository = divisionRepository;
	}

	/// <summary>
	/// Gets a paginated list of students in admission stage with filters
	/// </summary>
	public async Task<PagedResultDto<StudentAdmissionListItemDto>> GetListAsync(GetStudentAdmissionsInput input)
	{
		var query = (await _studentRepository.GetQueryableAsync())
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

		if (!string.IsNullOrWhiteSpace(input.ApprovalStatus) && 
			Enum.TryParse<StudentEnrollmentApprovalState>(input.ApprovalStatus, out var status))
		{
			query = query.Where(s => s.AdmissionApprovalStatus == status);
		}

		if (input.DivisionId.HasValue)
		{
			query = query.Where(s => s.DivisionId == input.DivisionId.Value);
		}

		if (input.StandardId.HasValue)
		{
			query = query.Where(s => s.StandardId == input.StandardId.Value);
		}

		// Get total count
		var totalCount = await _studentRepository.AsyncExecuter.CountAsync(query);

		// Apply sorting and paging
		query = query.OrderByDescending(s => s.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		// Execute query and project to DTO
		var students = await _studentRepository.AsyncExecuter.ToListAsync(
			query.Select(s => new StudentAdmissionListItemDto
			{
				StudentId = s.Id,
				FirstName = s.FirstName,
				MiddleName = s.MiddleName,
				LastName = s.LastName,
				RegistrationNo = s.AdditionalDetail != null ? s.AdditionalDetail.RegistrationFormNo : null,
				DivisionId = s.DivisionId,
				StandardId = s.StandardId,
				ApprovalStatus = s.AdmissionApprovalStatus.ToString(),
				ApprovalRemarks = s.AdmissionApprovalRemarks,
				RollNo = s.RollNo,
				GrNo = s.GrNo
			})
		);

		return new PagedResultDto<StudentAdmissionListItemDto>(totalCount, students);
	}

	/// <summary>
	/// Assigns or changes division for a student
	/// </summary>
	public async Task AssignDivisionAsync(Guid studentId, AssignDivisionDto input)
	{
		// Validate division exists
		var divisionExists = await _divisionRepository.AnyAsync(d => d.Id == input.DivisionId);
		if (!divisionExists)
		{
			throw new BusinessException("StudentAdmission:DivisionNotFound")
				.WithData("DivisionId", input.DivisionId);
		}

		// Get student
		var student = await _studentRepository.GetAsync(studentId);

		// Assign division using domain method
		student.AssignDivision(input.DivisionId);

		// Save changes
		await _studentRepository.UpdateAsync(student);
	}

	/// <summary>
	/// Changes admission status (approve/reject) for multiple students
	/// </summary>
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
				var student = await _studentRepository.GetAsync(item.StudentId);

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

				// Save changes
				await _studentRepository.UpdateAsync(student);

				itemResult.Success = true;
				result.Succeeded++;
			}
			catch (Exception ex)
			{
				itemResult.Success = false;
				itemResult.Message = ex.Message;
				result.Failed++;
			}

			result.Results.Add(itemResult);
		}

		return result;
	}
}
