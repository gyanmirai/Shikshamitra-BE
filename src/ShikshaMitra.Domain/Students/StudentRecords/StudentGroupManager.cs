using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupManager : DomainService
{
	public async Task<StudentGroupMembershipResult> ValidateStudentGroupAssignmentAsync(
		Student student,
		StudentGroup group,
		bool isLeader = false,
		StudentGroupMember existingMembership = null)
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
				student.TenantId
			);

			if (isLeader)
			{
				result.NeedsLeaderUpdate = true;
			}
		}

		return result;
	}

	public StudentGroupMember CreateGroupMembership(
		Guid studentId,
		Guid groupId,
		bool isLeader,
		Guid? tenantId) => new StudentGroupMember(
			Guid.NewGuid(),
			studentId,
			groupId,
			isLeader,
			tenantId
		);

	public async Task<StudentGroupMember> PrepareLeadershipUpdateAsync(
		Student student,
		StudentGroup group,
		StudentGroupMember existingMembership)
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
				student.TenantId
			);
		}
	}
}

public class StudentGroupMembershipResult
{
	public bool NeedsNewMembership { get; set; }
	public StudentGroupMember NewMembership { get; set; }

	public bool UpdateExistingMembership { get; set; }
	public StudentGroupMember ExistingMembership { get; set; }

	public bool NeedsLeaderUpdate { get; set; }
}