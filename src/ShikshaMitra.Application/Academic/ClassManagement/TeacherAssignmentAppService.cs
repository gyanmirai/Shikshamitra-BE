using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

[RemoteService(IsEnabled = false)]
public class TeacherAssignmentAppService(
	IRepository<TeacherSubjectAllocation, Guid> allocationRepository,
	IRepository<StandardSubjectAssignment, Guid> subjectAssignmentRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Employee, Guid> employeeRepository,
	IIdentityUserRepository userRepository,
	IdentityUserManager identityUserManager,
	IDistributedCache<List<TeacherInfoDto>> teacherCache,
	IGuidGenerator guidGenerator,
	ILogger<TeacherAssignmentAppService> logger
) : ApplicationService, ITeacherAssignmentAppService
{
	public async Task<TeacherAssignmentGridResponseDto> GetTeacherAssignmentsForClassAsync(
		Guid academicMediumId,
		Guid classId
	)
	{
		logger.LogInformation(
			"Getting teacher assignments for class {ClassId}",
			classId
		);

		// Get the standard/class details
		var standard = await standardRepository.GetAsync(classId);

		// Get all divisions for the class
		var divisions = await divisionRepository.GetListAsync(d => d.StandardId == classId);

		// Get all subject assignments for the class
		var assignments = await subjectAssignmentRepository.GetListAsync(a =>
			a.StandardId == classId &&
			a.EducationMediumId == academicMediumId
		);

		var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));

		// Get all teacher allocations for this class
		var divisionIds = divisions.Select(d => d.Id).ToList();
		var allocations = await allocationRepository.GetListAsync(a =>
			a.StandardId == classId &&
			divisionIds.Contains(a.DivisionId) &&
			subjectIds.Contains(a.SubjectId),
			includeDetails: true
		);

		// Get all teacher IDs
		var teacherIds = allocations
			.SelectMany(a => a.Teachers.Select(t => t.TeacherId))
			.Distinct()
			.ToList();

		var employees = teacherIds.Any()
			? await employeeRepository.GetListAsync(e => teacherIds.Contains(e.Id))
			: new List<Employee>();
		var userIds = employees
			.Select(e => e.UserId)
			.Where(id => id.HasValue)
			.Select(id => id!.Value)
			.Distinct()
			.ToList();
		var users = userIds.Any()
			? await AsyncExecuter.ToListAsync(
				identityUserManager.Users.Where(u => userIds.Contains(u.Id))
			)
			: new List<IdentityUser>();
		var employeeLookup = employees.ToDictionary(e => e.Id);
		var userLookup = users.ToDictionary(u => u.Id);

		var result = new TeacherAssignmentGridResponseDto
		{
			ClassName = standard.Name,
			Divisions = divisions.Select(division =>
			{
				return new DivisionRowDto
				{
					DivisionId = division.Id,
					DivisionName = division.Name,
					Subjects = subjects.Select(subject =>
					{
						var assignment = assignments.FirstOrDefault(a => a.SubjectId == subject.Id);
						var allocation = allocations.FirstOrDefault(a =>
							a.DivisionId == division.Id &&
							a.SubjectId == subject.Id
						);

						var assignedTeachers = new List<TeacherInfoDto>();

						if (allocation != null && allocation.Teachers.Any())
						{
					assignedTeachers = allocation.Teachers.Select(t =>
					{
						employeeLookup.TryGetValue(t.TeacherId, out var employee);
						IdentityUser? user = null;
						if (employee?.UserId != null)
						{
							userLookup.TryGetValue(employee.UserId.Value, out user);
						}

						var teacherName = string.Join(
							" ",
							new[] { user?.Name, user?.Surname }
								.Where(n => !string.IsNullOrWhiteSpace(n))
						);
						if (string.IsNullOrWhiteSpace(teacherName))
						{
							teacherName = employee?.EmployeeName ?? "Unknown";
						}

						return new TeacherInfoDto
						{
							TeacherId = t.TeacherId,
							TeacherName = string.IsNullOrWhiteSpace(teacherName) ? "Unknown" : teacherName
						};
					}).ToList();
						}

						return new DivisionSubjectAssignmentDto
						{
							SubjectId = subject.Id,
							SubjectName = subject.Name,
							SubjectCode = assignment?.SubjectCode ?? subject.Name,
							AssignedTeachers = assignedTeachers
						};
					}).ToList()
				};
			}).ToList()
		};

		return result;
	}

	public async Task<ApiSuccessResponseDto> SaveTeacherAssignmentsForClassAsync(
		SaveTeacherAssignmentsDto input
	)
	{
		logger.LogInformation(
			"Saving teacher assignments for class {ClassId}",
			input.ClassId
		);

		try
		{
			// Batch load all required data before processing to avoid N+1 queries
			var allTeacherIds = input.Assignments
				.SelectMany(a => a.TeacherIds)
				.Distinct()
				.ToList();

			// Validate all teachers exist
			var employees = await employeeRepository.GetListAsync(e => allTeacherIds.Contains(e.Id));
			var employeeIds = employees.Select(e => e.Id).ToHashSet();
			
			var missingTeachers = allTeacherIds.Where(id => !employeeIds.Contains(id)).ToList();
			if (missingTeachers.Any())
			{
				throw new BusinessException("ClassManagement:TeacherNotFound")
					.WithData("TeacherIds", string.Join(", ", missingTeachers));
			}

			// Batch load all existing allocations for this class
			var divisionIds = input.Assignments.Select(a => a.DivisionId).Distinct().ToList();
			var subjectIds = input.Assignments.Select(a => a.SubjectId).Distinct().ToList();
			
			var existingAllocations = await allocationRepository.GetListAsync(
				a => a.StandardId == input.ClassId &&
					divisionIds.Contains(a.DivisionId) &&
					subjectIds.Contains(a.SubjectId),
				includeDetails: true
			);
			
			// Create lookup for O(1) access
			var allocationLookup = existingAllocations.ToDictionary(
				a => (a.DivisionId, a.SubjectId)
			);

			foreach (var assignment in input.Assignments)
			{
				// Find existing allocation from in-memory lookup
				allocationLookup.TryGetValue(
					(assignment.DivisionId, assignment.SubjectId),
					out var existingAllocation
				);

				if (existingAllocation != null)
				{
					// Update existing allocation - remove old teachers and add new ones
					// Get current teacher IDs
					var currentTeacherIds = existingAllocation.Teachers.Select(t => t.TeacherId).ToHashSet();
					var newTeacherIds = assignment.TeacherIds.ToHashSet();
					
					// Remove teachers that are no longer assigned
					var teachersToRemove = existingAllocation.Teachers
						.Where(t => !newTeacherIds.Contains(t.TeacherId))
						.ToList();
					foreach (var teacher in teachersToRemove)
					{
						existingAllocation.Teachers.Remove(teacher);
					}
					
					// Add new teachers that aren't already assigned
					var teachersToAdd = newTeacherIds.Except(currentTeacherIds);
					foreach (var teacherId in teachersToAdd)
					{
						existingAllocation.Teachers.Add(new AllocatedTeacher(
							existingAllocation.Id,
							teacherId
						));
					}
					await allocationRepository.UpdateAsync(existingAllocation);
				}
			else
			{
				// Create new allocation with proper constructor
				var allocationId = guidGenerator.Create();
				var newAllocation = new TeacherSubjectAllocation(
					allocationId,
					input.ClassId,
					assignment.DivisionId,
					assignment.SubjectId
				);
				
				// Add teachers
				foreach (var teacherId in assignment.TeacherIds)
				{
					newAllocation.Teachers.Add(new AllocatedTeacher(allocationId, teacherId));
				}
				
				await allocationRepository.InsertAsync(newAllocation);
			}
			}

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = $"Successfully saved {input.Assignments.Count} teacher assignments."
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to save teacher assignments");
			throw new UserFriendlyException(
				"Failed to save teacher assignments. Please try again later."
			);
		}
	}

	public async Task<List<TeacherInfoDto>> GetTeachersAsync(string? search = null)
	{
		logger.LogInformation("Getting teachers list with search: {Search}", search);

		// Try to get from cache if no search filter
		if (string.IsNullOrWhiteSpace(search))
		{
			var cacheKey = $"Teachers_All_{CurrentTenant.Id}";
			var cachedTeachers = await teacherCache.GetAsync(cacheKey);
			if (cachedTeachers != null)
			{
				logger.LogDebug("Returning teachers from cache");
				return cachedTeachers;
			}
		}

		// Get all employees who are teachers
		// Filter by Teacher role or TeachingStaff employee group
		var employeesQuery = await employeeRepository.GetQueryableAsync();
		
		// Apply teacher filtering - adjust based on your domain requirements:
		// Option 1: Filter by EmployeeGroup if TeachingStaff is used for teachers
		// Option 2: Filter by Role if you have a "Teacher" role
		// For now, filtering by EmployeeGroup.TeachingStaff as it's the most common pattern
		employeesQuery = employeesQuery.Where(e => 
			e.EmployeeGroup == EmployeeGroupType.Teaching
		);
		
		var employees = await AsyncExecuter.ToListAsync(employeesQuery);

		// Extract user IDs and query only those users (not all users)
		var userIds = employees
			.Where(e => e.UserId.HasValue)
			.Select(e => e.UserId!.Value)
			.Distinct()
			.ToList();

		if (!userIds.Any())
		{
			return new List<TeacherInfoDto>();
		}

		// Query only the required users instead of loading all users
		var usersQuery = identityUserManager.Users.Where(u => userIds.Contains(u.Id));

		if (!string.IsNullOrWhiteSpace(search))
		{
			var searchLower = search.ToLower();
			usersQuery = usersQuery.Where(u =>
				u.Name.ToLower().Contains(searchLower) ||
				u.Surname.ToLower().Contains(searchLower) ||
				u.UserName.ToLower().Contains(searchLower)
			);
		}

		var users = await AsyncExecuter.ToListAsync(usersQuery);
		
		// Filter employees to only those with non-null UserId before creating dictionary
		// This prevents NullReferenceException when employees have null UserId
		var employeeLookup = employees
			.Where(e => e.UserId.HasValue)
			.ToDictionary(e => e.UserId!.Value);

		var result = users
			.Where(u => employeeLookup.ContainsKey(u.Id))
			.Select(user => new TeacherInfoDto
			{
				TeacherId = employeeLookup[user.Id].Id,
				TeacherName = $"{user.Name} {user.Surname}".Trim()
			})
			.ToList();

		// Cache the result if no search filter (cache for 1 hour)
		if (string.IsNullOrWhiteSpace(search))
		{
			var cacheKey = $"Teachers_All_{CurrentTenant.Id}";
			await teacherCache.SetAsync(
				cacheKey,
				result,
				new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
				}
			);
		}

		return result;
	}
}
