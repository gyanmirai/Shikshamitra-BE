using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
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
	IRepository<AssignedSubject, Guid> subjectAssignmentRepository,
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
			"Getting teacher assignments for class {ClassId} and academic medium {AcademicMediumId}",
			classId,
			academicMediumId
		);

		try
		{
			// Validate input parameters
			if (classId == Guid.Empty)
			{
				logger.LogWarning("Invalid classId provided: {ClassId}", classId);
				throw new UserFriendlyException(
					"Invalid class ID provided.",
					code: "ClassManagement:InvalidClassId"
				);
			}

			if (academicMediumId == Guid.Empty)
			{
				logger.LogWarning("Invalid academicMediumId provided: {AcademicMediumId}", academicMediumId);
				throw new UserFriendlyException(
					"Invalid academic medium ID provided.",
					code: "ClassManagement:InvalidAcademicMediumId"
				);
			}

			// Validate that the standard/class exists
			Standard? standard = null;
			try
			{
				standard = await standardRepository.FindAsync(classId);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving standard with ID {ClassId}", classId);
				throw new UserFriendlyException(
					"Unable to retrieve class information. Please contact support if the issue persists.",
					code: "ClassManagement:DatabaseError"
				);
			}

			if (standard == null)
			{
				logger.LogWarning("Standard with ID {ClassId} not found", classId);
				throw new UserFriendlyException(
					$"Class not found with ID: {classId}",
					code: "ClassManagement:ClassNotFound"
				);
			}

			// Get all divisions for the class
			List<Division> divisions;
			try
			{
				divisions = await divisionRepository.GetListAsync(d => d.StandardId == classId);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving divisions for class {ClassId}", classId);
				throw new UserFriendlyException(
					"Unable to retrieve divisions. Please contact support if the issue persists.",
					code: "ClassManagement:DatabaseError"
				);
			}

			if (divisions == null || !divisions.Any())
			{
				logger.LogInformation("No divisions found for class {ClassId}. Returning empty result.", classId);
				return new TeacherAssignmentGridResponseDto
				{
					ClassName = standard.Name,
					Divisions = new List<DivisionRowDto>()
				};
			}

			// Get all subject assignments for the class
			List<AssignedSubject> assignments;
			try
			{
				assignments = await subjectAssignmentRepository.GetListAsync(a =>
					a.StandardId == classId &&
					a.EducationMediumId == academicMediumId
				);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving subject assignments for class {ClassId}", classId);
				throw new UserFriendlyException(
					"Unable to retrieve subject assignments. Please contact support if the issue persists.",
					code: "ClassManagement:DatabaseError"
				);
			}

			if (assignments == null || !assignments.Any())
			{
				logger.LogInformation(
					"No subject assignments found for class {ClassId} and academic medium {AcademicMediumId}. Returning empty result.",
					classId,
					academicMediumId
				);
				return new TeacherAssignmentGridResponseDto
				{
					ClassName = standard.Name,
					Divisions = divisions.Select(d => new DivisionRowDto
					{
						DivisionId = d.Id,
						DivisionName = d.Name,
						Subjects = new List<DivisionSubjectAssignmentDto>()
					}).ToList()
				};
			}

			var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
			
			List<Subject> subjects;
			try
			{
				subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving subjects for class {ClassId}", classId);
				throw new UserFriendlyException(
					"Unable to retrieve subjects. Please contact support if the issue persists.",
					code: "ClassManagement:DatabaseError"
				);
			}

			// Get all teacher allocations for this class - handle table not existing
			var divisionIds = divisions.Select(d => d.Id).ToList();
			List<TeacherSubjectAllocation> allocations = new List<TeacherSubjectAllocation>();
			
			try
			{
				allocations = await allocationRepository.GetListAsync(a =>
					a.StandardId == classId &&
					divisionIds.Contains(a.DivisionId) &&
					subjectIds.Contains(a.SubjectId),
					includeDetails: true
				);
			}
			catch (Exception ex) when (ex.Message.Contains("42P01") || 
			                           ex.Message.Contains("does not exist") ||
			                           ex.InnerException?.Message.Contains("42P01") == true ||
			                           ex.InnerException?.Message.Contains("does not exist") == true)
			{
				logger.LogWarning(
					ex,
					"Teacher allocation table does not exist. Returning empty allocations. Exception: {ExceptionType}",
					ex.GetType().Name
				);
				// Return empty allocations - table hasn't been created yet
				allocations = new List<TeacherSubjectAllocation>();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving teacher allocations for class {ClassId}", classId);
				// Don't fail the entire request - just return empty allocations
				allocations = new List<TeacherSubjectAllocation>();
			}

			// Get all teacher IDs
			var teacherIds = allocations
				.Where(a => a.Teachers != null)
				.SelectMany(a => a.Teachers.Select(t => t.TeacherId))
				.Distinct()
				.ToList();

			List<Employee> employees = new List<Employee>();
			List<IdentityUser> users = new List<IdentityUser>();
			
			if (teacherIds.Any())
			{
				try
				{
					employees = await employeeRepository.GetListAsync(e => teacherIds.Contains(e.Id));
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error retrieving employees for teacher allocations");
					employees = new List<Employee>();
				}

				if (employees.Any())
				{
					var userIds = employees
						.Where(e => e.UserId.HasValue)
						.Select(e => e.UserId!.Value)
						.Distinct()
						.ToList();

					if (userIds.Any())
					{
						try
						{
							users = await AsyncExecuter.ToListAsync(
								identityUserManager.Users.Where(u => userIds.Contains(u.Id))
							);
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Error retrieving users for teacher allocations");
							users = new List<IdentityUser>();
						}
					}
				}
			}

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

							if (allocation != null && allocation.Teachers != null && allocation.Teachers.Any())
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

			logger.LogInformation(
				"Successfully retrieved teacher assignments for class {ClassId}. Found {DivisionCount} divisions and {SubjectCount} subjects.",
				classId,
				divisions.Count,
				subjects.Count
			);

			return result;
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected error while getting teacher assignments for class {ClassId}", classId);
			throw new UserFriendlyException(
				"An unexpected error occurred while retrieving teacher assignments. Please try again later.",
				code: "ClassManagement:UnexpectedError"
			);
		}
	}

	public async Task<ApiSuccessResponseDto> SaveTeacherAssignmentsForClassAsync(
		SaveTeacherAssignmentsDto input
	)
	{
		logger.LogInformation(
			"Saving teacher assignments for class {ClassId}. Total assignments: {AssignmentCount}",
			input.ClassId,
			input.Assignments?.Count ?? 0
		);

		try
		{
			// Validate input
			if (input == null)
			{
				logger.LogWarning("Null input provided to SaveTeacherAssignmentsForClassAsync");
				throw new UserFriendlyException(
					"Invalid input provided.",
					code: "ClassManagement:InvalidInput"
				);
			}

			if (input.ClassId == Guid.Empty)
			{
				logger.LogWarning("Invalid ClassId provided: {ClassId}", input.ClassId);
				throw new UserFriendlyException(
					"Invalid class ID provided.",
					code: "ClassManagement:InvalidClassId"
				);
			}

			if (input.Assignments == null || !input.Assignments.Any())
			{
				logger.LogWarning("No assignments provided for class {ClassId}", input.ClassId);
				throw new UserFriendlyException(
					"No assignments provided.",
					code: "ClassManagement:NoAssignments"
				);
			}

			// Validate that the class exists
			Standard? classExists = null;
			try
			{
				classExists = await standardRepository.FindAsync(input.ClassId);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error checking if standard exists with ID {ClassId}", input.ClassId);
				throw new UserFriendlyException(
					"Unable to verify class information. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}

			if (classExists == null)
			{
				logger.LogWarning("Standard with ID {ClassId} not found", input.ClassId);
				throw new UserFriendlyException(
					$"Class not found with ID: {input.ClassId}",
					code: "ClassManagement:ClassNotFound"
				);
			}

			// Batch load all required data before processing to avoid N+1 queries
			var allTeacherIds = input.Assignments
				.Where(a => a.TeacherIds != null)
				.SelectMany(a => a.TeacherIds)
				.Distinct()
				.ToList();

			// Validate all teachers exist
			List<Employee> employees;
			try
			{
				employees = await employeeRepository.GetListAsync(e => allTeacherIds.Contains(e.Id));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving employees for validation");
				throw new UserFriendlyException(
					"Unable to validate teacher information. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}

			var employeeIds = employees.Select(e => e.Id).ToHashSet();
			
			var missingTeachers = allTeacherIds.Where(id => !employeeIds.Contains(id)).ToList();
			if (missingTeachers.Any())
			{
				logger.LogWarning(
					"Teachers not found: {MissingTeachers}",
					string.Join(", ", missingTeachers)
				);
				throw new UserFriendlyException(
					$"One or more teachers not found. Invalid teacher IDs: {string.Join(", ", missingTeachers)}",
					code: "ClassManagement:TeacherNotFound"
				);
			}

			// Validate divisions exist
			var divisionIds = input.Assignments.Select(a => a.DivisionId).Distinct().ToList();
			
			List<Division> divisions;
			try
			{
				divisions = await divisionRepository.GetListAsync(d => 
					divisionIds.Contains(d.Id) && d.StandardId == input.ClassId);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving divisions for validation");
				throw new UserFriendlyException(
					"Unable to validate division information. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}

			var validDivisionIds = divisions.Select(d => d.Id).ToHashSet();
			var invalidDivisions = divisionIds.Where(id => !validDivisionIds.Contains(id)).ToList();
			
			if (invalidDivisions.Any())
			{
				logger.LogWarning(
					"Divisions not found or don't belong to class {ClassId}: {InvalidDivisions}",
					input.ClassId,
					string.Join(", ", invalidDivisions)
				);
				throw new UserFriendlyException(
					$"One or more divisions not found or don't belong to this class. Invalid division IDs: {string.Join(", ", invalidDivisions)}",
					code: "ClassManagement:DivisionNotFound"
				);
			}

			// Validate subjects exist
			var subjectIds = input.Assignments.Select(a => a.SubjectId).Distinct().ToList();
			
			List<Subject> subjects;
			try
			{
				subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving subjects for validation");
				throw new UserFriendlyException(
					"Unable to validate subject information. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}

			var validSubjectIds = subjects.Select(s => s.Id).ToHashSet();
			var invalidSubjects = subjectIds.Where(id => !validSubjectIds.Contains(id)).ToList();
			
			if (invalidSubjects.Any())
			{
				logger.LogWarning(
					"Subjects not found: {InvalidSubjects}",
					string.Join(", ", invalidSubjects)
				);
				throw new UserFriendlyException(
					$"One or more subjects not found. Invalid subject IDs: {string.Join(", ", invalidSubjects)}",
					code: "ClassManagement:SubjectNotFound"
				);
			}

			// Batch load all existing allocations for this class
			List<TeacherSubjectAllocation> existingAllocations;
			try
			{
				existingAllocations = await allocationRepository.GetListAsync(
					a => a.StandardId == input.ClassId &&
						divisionIds.Contains(a.DivisionId) &&
						subjectIds.Contains(a.SubjectId),
					includeDetails: true
				);
			}
			catch (Exception ex) when (ex.Message.Contains("42P01") || 
			                           ex.Message.Contains("does not exist") ||
			                           ex.InnerException?.Message.Contains("42P01") == true ||
			                           ex.InnerException?.Message.Contains("does not exist") == true)
			{
				logger.LogError(
					ex,
					"Teacher allocation table does not exist. Please run database migrations. Exception: {ExceptionType}",
					ex.GetType().Name
				);
				throw new UserFriendlyException(
					"The teacher allocation feature is not properly configured. Please contact your system administrator to run database migrations.",
					code: "ClassManagement:TableNotFound"
				);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving existing allocations for class {ClassId}", input.ClassId);
				throw new UserFriendlyException(
					"Unable to retrieve existing teacher allocations. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}
			
			// Create lookup for O(1) access
			var allocationLookup = existingAllocations.ToDictionary(
				a => (a.DivisionId, a.SubjectId)
			);

			int updatedCount = 0;
			int createdCount = 0;

			foreach (var assignment in input.Assignments)
			{
				try
				{
					// Validate assignment has teacher IDs
					if (assignment.TeacherIds == null || !assignment.TeacherIds.Any())
					{
						logger.LogDebug(
							"Skipping assignment for Division {DivisionId} and Subject {SubjectId} - no teachers assigned",
							assignment.DivisionId,
							assignment.SubjectId
						);
						continue;
					}

					// Find existing allocation from in-memory lookup
					allocationLookup.TryGetValue(
						(assignment.DivisionId, assignment.SubjectId),
						out var existingAllocation
					);

					if (existingAllocation != null)
					{
						// Update existing allocation - remove old teachers and add new ones
						var currentTeacherIds = existingAllocation.Teachers?.Select(t => t.TeacherId).ToHashSet() ?? new HashSet<Guid>();
						var newTeacherIds = assignment.TeacherIds.ToHashSet();
						
						// Remove teachers that are no longer assigned
						var teachersToRemove = existingAllocation.Teachers?
							.Where(t => !newTeacherIds.Contains(t.TeacherId))
							.ToList() ?? new List<AllocatedTeacher>();
						
						foreach (var teacher in teachersToRemove)
						{
							existingAllocation.Teachers?.Remove(teacher);
						}
						
						// Add new teachers that aren't already assigned
						var teachersToAdd = newTeacherIds.Except(currentTeacherIds);
						foreach (var teacherId in teachersToAdd)
						{
							existingAllocation.Teachers?.Add(new AllocatedTeacher(
								existingAllocation.Id,
								teacherId
							));
						}
						
						await allocationRepository.UpdateAsync(existingAllocation);
						updatedCount++;
						
						logger.LogDebug(
							"Updated allocation for Division {DivisionId}, Subject {SubjectId}",
							assignment.DivisionId,
							assignment.SubjectId
						);
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
						createdCount++;
						
						logger.LogDebug(
							"Created new allocation for Division {DivisionId}, Subject {SubjectId}",
							assignment.DivisionId,
							assignment.SubjectId
						);
					}
				}
				catch (Exception ex)
				{
					logger.LogError(
						ex,
						"Error processing assignment for Division {DivisionId}, Subject {SubjectId}",
						assignment.DivisionId,
						assignment.SubjectId
					);
					throw new UserFriendlyException(
						$"Failed to save assignment for division {assignment.DivisionId} and subject {assignment.SubjectId}.",
						code: "ClassManagement:AssignmentSaveFailed"
					);
				}
			}

			logger.LogInformation(
				"Successfully saved teacher assignments for class {ClassId}. Created: {CreatedCount}, Updated: {UpdatedCount}",
				input.ClassId,
				createdCount,
				updatedCount
			);

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = $"Successfully saved {input.Assignments.Count} teacher assignments ({createdCount} created, {updatedCount} updated)."
			};
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected error while saving teacher assignments for class {ClassId}", input?.ClassId);
			throw new UserFriendlyException(
				"An unexpected error occurred while saving teacher assignments. Please try again later.",
				code: "ClassManagement:UnexpectedError"
			);
		}
	}

	public async Task<List<TeacherInfoDto>> GetTeachersAsync(string? search = null)
	{
		logger.LogInformation("Getting teachers list with search: {Search}", search ?? "(none)");

		try
		{
			// Try to get from cache if no search filter
			if (string.IsNullOrWhiteSpace(search))
			{
				try
				{
					var cacheKey = $"Teachers_All_{CurrentTenant.Id}";
					var cachedTeachers = await teacherCache.GetAsync(cacheKey);
					if (cachedTeachers != null && cachedTeachers.Any())
					{
						logger.LogDebug("Returning {Count} teachers from cache", cachedTeachers.Count);
						return cachedTeachers;
					}
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Failed to retrieve teachers from cache. Proceeding with database query.");
				}
			}

			// Get all employees who are teachers
			IQueryable<Employee> employeesQuery;
			try
			{
				employeesQuery = await employeeRepository.GetQueryableAsync();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error getting employee queryable");
				throw new UserFriendlyException(
					"Unable to retrieve teacher information. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}
			
			// Filter by EmployeeGroup.Teaching
			employeesQuery = employeesQuery.Where(e => 
				e.EmployeeGroup == EmployeeGroupType.Teaching
			);
			
			List<Employee> employees;
			try
			{
				employees = await AsyncExecuter.ToListAsync(employeesQuery);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving teaching staff employees");
				throw new UserFriendlyException(
					"Unable to retrieve teachers. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}

			if (employees == null || !employees.Any())
			{
				logger.LogWarning("No teaching staff found in the system");
				return new List<TeacherInfoDto>();
			}

			// Extract user IDs and query only those users (not all users)
			var userIds = employees
				.Where(e => e.UserId.HasValue)
				.Select(e => e.UserId!.Value)
				.Distinct()
				.ToList();

			if (!userIds.Any())
			{
				logger.LogWarning("No teachers found with valid user IDs");
				return new List<TeacherInfoDto>();
			}

			// Query only the required users instead of loading all users
			var usersQuery = identityUserManager.Users.Where(u => userIds.Contains(u.Id));

			if (!string.IsNullOrWhiteSpace(search))
			{
				var searchLower = search.ToLower();
				usersQuery = usersQuery.Where(u =>
					(u.Name != null && u.Name.ToLower().Contains(searchLower)) ||
					(u.Surname != null && u.Surname.ToLower().Contains(searchLower)) ||
					(u.UserName != null && u.UserName.ToLower().Contains(searchLower))
				);
			}

			List<IdentityUser> users;
			try
			{
				users = await AsyncExecuter.ToListAsync(usersQuery);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving users for teachers");
				throw new UserFriendlyException(
					"Unable to retrieve teacher details. Please try again later.",
					code: "ClassManagement:DatabaseError"
				);
			}
			
			// Filter employees to only those with non-null UserId before creating dictionary
			var employeeLookup = employees
				.Where(e => e.UserId.HasValue)
				.ToDictionary(e => e.UserId!.Value);

			var result = users
				.Where(u => employeeLookup.ContainsKey(u.Id))
				.Select(user => new TeacherInfoDto
				{
					TeacherId = employeeLookup[user.Id].Id,
					TeacherName = $"{user.Name ?? ""} {user.Surname ?? ""}".Trim()
				})
				.Where(t => !string.IsNullOrWhiteSpace(t.TeacherName))
				.OrderBy(t => t.TeacherName)
				.ToList();

			// Cache the result if no search filter (cache for 1 hour)
			if (string.IsNullOrWhiteSpace(search) && result.Any())
			{
				try
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
					logger.LogDebug("Cached {Count} teachers for tenant {TenantId}", result.Count, CurrentTenant.Id);
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Failed to cache teacher list. Continuing without caching.");
				}
			}

			logger.LogInformation("Successfully retrieved {TeacherCount} teachers", result.Count);
			return result;
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected error while retrieving teachers list");
			throw new UserFriendlyException(
				"An unexpected error occurred while retrieving teachers. Please try again later.",
				code: "ClassManagement:UnexpectedError"
			);
		}
	}
}
