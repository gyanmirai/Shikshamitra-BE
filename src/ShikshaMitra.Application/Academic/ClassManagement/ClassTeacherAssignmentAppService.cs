using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

[RemoteService(IsEnabled = false)]
public class ClassTeacherAssignmentAppService(
	IRepository<ClassTeacher.ClassTeacher, Guid> classTeacherRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Employee, Guid> employeeRepository,
	IdentityUserManager identityUserManager,
	IGuidGenerator guidGenerator,
	ILogger<ClassTeacherAssignmentAppService> logger
) : ApplicationService, IClassTeacherAssignmentAppService
{
	public async Task<List<ClassTeacherRowDto>> GetClassTeacherAssignmentsAsync(
		Guid organizationId
	)
	{
		logger.LogInformation(
			"Getting class teacher assignments for organization {OrganizationId}",
			organizationId
		);

		// Filter standards by EducationMediumStageId (organizationId)
		var standards = await standardRepository.GetListAsync(s => s.EducationMediumStageId == organizationId);

		// If no standards found for this organization, return empty list
		if (!standards.Any())
		{
			logger.LogWarning("No standards found for organization {OrganizationId}", organizationId);
			return new List<ClassTeacherRowDto>();
		}

		// Get standard IDs to filter related data
		var standardIds = standards.Select(s => s.Id).ToList();

		// Get only divisions for the filtered standards
		var divisions = await divisionRepository.GetListAsync(d => standardIds.Contains(d.StandardId));

		// Get only class teacher assignments for the filtered standards
		var classTeachers = await classTeacherRepository.GetListAsync(ct => standardIds.Contains(ct.StandardId));

		// Early return if no class teachers
		if (!classTeachers.Any())
		{
			// Return empty assignments for all class-division combinations
			var emptyResult = new List<ClassTeacherRowDto>();
			foreach (var standard in standards)
			{
				var standardDivisions = divisions.Where(d => d.StandardId == standard.Id);
				foreach (var division in standardDivisions)
				{
					emptyResult.Add(new ClassTeacherRowDto
					{
						ClassId = standard.Id,
						ClassName = standard.Name,
						DivisionId = division.Id,
						DivisionName = division.Name,
						AssignedTeacher = null
					});
				}
			}
			return emptyResult;
		}

		// Get employees who are assigned as class teachers with predicate filtering
		var teacherIds = classTeachers.Select(ct => ct.TeacherId).Distinct().ToList();
		var employees = await employeeRepository.GetListAsync(e => teacherIds.Contains(e.Id));
		
		// Get only the required users using IdentityUserManager queryable
		var userIds = employees.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).Distinct().ToList();
		var users = userIds.Any() 
			? await AsyncExecuter.ToListAsync(identityUserManager.Users.Where(u => userIds.Contains(u.Id)))
			: new List<IdentityUser>();

		var result = new List<ClassTeacherRowDto>();

		foreach (var standard in standards)
		{
			var standardDivisions = divisions.Where(d => d.StandardId == standard.Id);

			foreach (var division in standardDivisions)
			{
				var classTeacher = classTeachers.FirstOrDefault(ct =>
					ct.StandardId == standard.Id &&
					ct.DivisionId == division.Id
				);

				AssignedTeacherDto? assignedTeacher = null;

				if (classTeacher != null)
				{
					var employee = employees.FirstOrDefault(e => e.Id == classTeacher.TeacherId);
					var user = employee != null
						? users.FirstOrDefault(u => u.Id == employee.UserId)
						: null;

					if (user != null)
					{
						assignedTeacher = new AssignedTeacherDto
						{
							TeacherId = classTeacher.TeacherId,
							TeacherName = $"{user.Name} {user.Surname}".Trim()
						};
					}
				}

				result.Add(new ClassTeacherRowDto
				{
					ClassId = standard.Id,
					ClassName = standard.Name,
					DivisionId = division.Id,
					DivisionName = division.Name,
					AssignedTeacher = assignedTeacher
				});
			}
		}

		return result;
	}

	public async Task<ApiSuccessResponseDto> SaveClassTeacherAssignmentsAsync(
		SaveClassTeacherAssignmentsDto input
	)
	{
		logger.LogInformation(
			"Saving class teacher assignments for organization {OrganizationId}",
			input.OrganizationId
		);

		try
		{
			// Batch load all required data before processing
			var teacherIds = input.Assignments.Select(a => a.TeacherId).Distinct().ToList();
			var classIds = input.Assignments.Select(a => a.ClassId).Distinct().ToList();
			var divisionIds = input.Assignments.Select(a => a.DivisionId).Distinct().ToList();

			// Verify all teachers exist
			var employees = await employeeRepository.GetListAsync(e => teacherIds.Contains(e.Id));
			var employeeIds = employees.Select(e => e.Id).ToHashSet();
			
			var missingTeachers = teacherIds.Where(id => !employeeIds.Contains(id)).ToList();
			if (missingTeachers.Any())
			{
				throw new BusinessException("ClassManagement:TeacherNotFound")
					.WithData("TeacherIds", string.Join(", ", missingTeachers));
			}

			// Verify all standards exist and get education medium stages
			var standards = await standardRepository.GetListAsync(s => classIds.Contains(s.Id));
			var standardIds = standards.Select(s => s.Id).ToHashSet();
			
			var missingStandards = classIds.Where(id => !standardIds.Contains(id)).ToList();
		if (missingStandards.Any())
		{
			throw new BusinessException("ClassManagement:StandardNotFound")
				.WithData("ClassIds", string.Join(", ", missingStandards));
		}

		// Verify all divisions exist
		var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
		var divisionIdsFound = divisions.Select(d => d.Id).ToHashSet();
		
		var missingDivisions = divisionIds.Where(id => !divisionIdsFound.Contains(id)).ToList();
		if (missingDivisions.Any())
		{
			throw new BusinessException("ClassManagement:DivisionNotFound")
				.WithData("DivisionIds", string.Join(", ", missingDivisions));
		}

		// Get all education medium stages
			var educationMediumStageIds = standards.Select(s => s.EducationMediumStageId).Distinct().ToList();
			var educationMediumStages = await educationMediumStageRepository.GetListAsync(
				ems => educationMediumStageIds.Contains(ems.Id)
			);
			var educationMediumStageLookup = educationMediumStages.ToDictionary(ems => ems.Id);
			var standardLookup = standards.ToDictionary(s => s.Id);

			// Get existing class teacher assignments
			var existingAssignments = await classTeacherRepository.GetListAsync(ct =>
				classIds.Contains(ct.StandardId) &&
				divisionIds.Contains(ct.DivisionId)
			);
			var existingLookup = existingAssignments.ToDictionary(
				ct => (ct.StandardId, ct.DivisionId)
			);

			// Process assignments
			foreach (var assignment in input.Assignments)
			{
				if (existingLookup.TryGetValue((assignment.ClassId, assignment.DivisionId), out var existingAssignment))
				{
					// Update existing assignment
					existingAssignment.TeacherId = assignment.TeacherId;
					await classTeacherRepository.UpdateAsync(existingAssignment);
				}
				else
				{
					// Create new assignment
					var standard = standardLookup[assignment.ClassId];
					var educationMediumStage = educationMediumStageLookup[standard.EducationMediumStageId];
					
					var newAssignment = new ClassTeacher.ClassTeacher(
						guidGenerator.Create(),
						CurrentTenant.Id,
						assignment.ClassId,
						assignment.DivisionId,
						assignment.TeacherId,
						educationMediumStage.EducationMediumId
					);
					
					await classTeacherRepository.InsertAsync(newAssignment);
				}
			}

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = "Class teacher assignments saved successfully."
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to save class teacher assignments");
			throw new UserFriendlyException(
				"Failed to save class teacher assignments. Please try again later."
			);
		}
	}
}
