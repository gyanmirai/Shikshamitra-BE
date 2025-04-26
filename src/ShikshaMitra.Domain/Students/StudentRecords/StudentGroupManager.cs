using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupManager : DomainService
{
	public StudentGroupMembershipResult ValidateStudentGroupAssignmentAsync(
		Student student,
		StudentGroup group,
		bool isLeader = false,
		bool isDivisionLeader = false,
		StudentGroupMember? existingMembership = null)
	{
		var result = new StudentGroupMembershipResult();

		if (existingMembership != null)
		{
			// Student is already in this group
			if (isLeader && !existingMembership.IsLeader)
			{
				// Need to make this student a leader and remove existing leader
				result.NeedsLeaderUpdate = true;
				result.UpdateExistingMembership = true;
				result.ExistingMembership = existingMembership;
			}
			else if (!isLeader && existingMembership.IsLeader)
			{
				// Need to remove leader status
				existingMembership.RemoveAsLeader();
				result.UpdateExistingMembership = true;
				result.ExistingMembership = existingMembership;
			}

			// Division leader check
			if (isDivisionLeader && !existingMembership.IsDivisionLeader)
			{
				// Need to make this student a division leader and remove existing division leader
				result.NeedsDivisionLeaderUpdate = true;
				result.UpdateExistingMembership = true;
				result.ExistingMembership = existingMembership;
			}
			else if (!isDivisionLeader && existingMembership.IsDivisionLeader)
			{
				// Need to remove division leader status
				existingMembership.RemoveAsDivisionLeader();
				result.UpdateExistingMembership = true;
				result.ExistingMembership = existingMembership;
			}
		}
		else
		{
			// Student is not yet in this group
			result.NeedsNewMembership = true;
			result.NewMembership = new StudentGroupMember(
				Guid.NewGuid(),
				student.Id,
				group.Id,
				isLeader,
				isDivisionLeader,
				student.TenantId
			);

			if (isLeader)
			{
				result.NeedsLeaderUpdate = true;
			}

			if (isDivisionLeader)
			{
				result.NeedsDivisionLeaderUpdate = true;
			}
		}

		return result;
	}

	public StudentGroupMember CreateGroupMembership(
		Guid studentId,
		Guid groupId,
		bool isLeader,
		bool isDivisionLeader,
		Guid? tenantId) => new StudentGroupMember(
			Guid.NewGuid(),
			studentId,
			groupId,
			isLeader,
			isDivisionLeader,
			tenantId
		);

	public StudentGroupMember PrepareLeadershipUpdateAsync(
		Student student,
		StudentGroup group,
		StudentGroupMember? existingMembership)
	{
		if (existingMembership != null)
		{
			// Student is already in group, make them leader
			existingMembership.SetAsLeader();
			return existingMembership;
		}
		else
		{
			// Student is not in group, create new membership as leader
			return new StudentGroupMember(
				Guid.NewGuid(),
				student.Id,
				group.Id,
				true,
				false,
				student.TenantId
			);
		}
	}

	public StudentGroupMember PrepareDivisionLeadershipUpdateAsync(
		Student student,
		StudentGroup group,
		StudentGroupMember? existingMembership)
	{
		if (existingMembership != null)
		{
			// Student is already in group, make them division leader
			existingMembership.SetAsDivisionLeader();
			return existingMembership;
		}
		else
		{
			// Student is not in group, create new membership as division leader
			return new StudentGroupMember(
				Guid.NewGuid(),
				student.Id,
				group.Id,
				false,
				true,
				student.TenantId
			);
		}
	}

	/// <summary>
	/// Calculates optimal student group assignments based on division and gender balance
	/// </summary>
	/// <param name="students">List of students to assign to groups</param>
	/// <param name="groups">Available groups for assignment</param>
	/// <returns>Dictionary mapping each student to their optimal group</returns>
	public Dictionary<Student, StudentGroup> CalculateOptimalGroupAssignments(
		List<Student> students,
		List<StudentGroup> groups)
	{
		if (students == null || !students.Any() || groups == null || !groups.Any())
		{
			return new Dictionary<Student, StudentGroup>();
		}

		var groupAssignments = new Dictionary<Student, StudentGroup>();

		// 1. First separate boys and girls
		var boysStudents = students.Where(s => s.GenderType == GenderType.Male).ToList();
		var girlsStudents = students.Where(s => s.GenderType == GenderType.Female).ToList();

		// 2. Group students by division
		var boysByDivision = boysStudents
			.GroupBy(s => s.DivisionId)
			.Where(g => g.Key.HasValue) // Skip null division IDs
			.ToDictionary(g => g.Key!.Value, g => g.ToList());

		var girlsByDivision = girlsStudents
			.GroupBy(s => s.DivisionId)
			.Where(g => g.Key.HasValue) // Skip null division IDs
			.ToDictionary(g => g.Key!.Value, g => g.ToList());

		// 3. Process each division separately
		var allDivisionIds = new HashSet<Guid>();
		allDivisionIds.UnionWith(boysByDivision.Keys);
		allDivisionIds.UnionWith(girlsByDivision.Keys);

		foreach (var divisionId in allDivisionIds)
		{
			var hasBoys = boysByDivision.ContainsKey(divisionId) && boysByDivision[divisionId].Any();
			var hasGirls = girlsByDivision.ContainsKey(divisionId) && girlsByDivision[divisionId].Any();

			// Skip if no students in this division
			if (!hasBoys && !hasGirls) continue;

			// Get available groups
			var availableGroups = groups.ToList();
			if (!availableGroups.Any()) continue;

			// Define how many students per group
			var groupCount = availableGroups.Count;

			// Process boys
			if (hasBoys)
			{
				var boys = boysByDivision[divisionId];
				var boysPerGroup = boys.Count / groupCount;
				var extraBoys = boys.Count % groupCount;

				for (var i = 0; i < groupCount; i++)
				{
					var group = availableGroups[i];
					var currentBoysCount = boysPerGroup + (i < extraBoys ? 1 : 0);
					var boysForThisGroup = boys.Skip(i * boysPerGroup + Math.Min(i, extraBoys))
												.Take(currentBoysCount)
												.ToList();

					// Assign each boy to this group
					foreach (var student in boysForThisGroup)
					{
						groupAssignments[student] = group;
					}
				}
			}

			// Process girls
			if (hasGirls)
			{
				var girls = girlsByDivision[divisionId];
				var girlsPerGroup = girls.Count / groupCount;
				var extraGirls = girls.Count % groupCount;

				for (var i = 0; i < groupCount; i++)
				{
					var group = availableGroups[i];
					var currentGirlsCount = girlsPerGroup + (i < extraGirls ? 1 : 0);
					var girlsForThisGroup = girls.Skip(i * girlsPerGroup + Math.Min(i, extraGirls))
												 .Take(currentGirlsCount)
												 .ToList();

					// Assign each girl to this group
					foreach (var student in girlsForThisGroup)
					{
						groupAssignments[student] = group;
					}
				}
			}
		}

		return groupAssignments;
	}
}

public class StudentGroupMembershipResult
{
	public bool NeedsNewMembership { get; set; }
	public StudentGroupMember? NewMembership { get; set; }

	public bool UpdateExistingMembership { get; set; }
	public StudentGroupMember? ExistingMembership { get; set; }

	public bool NeedsLeaderUpdate { get; set; }
	public bool NeedsDivisionLeaderUpdate { get; set; }
}
