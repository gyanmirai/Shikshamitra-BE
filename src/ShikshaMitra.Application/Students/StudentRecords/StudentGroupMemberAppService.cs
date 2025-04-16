using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupMemberAppService(
	StudentGroupManager studentGroupManager,
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentGroup, Guid> studentGroupRepository,
	IRepository<StudentGroupMember, Guid> studentGroupMemberRepository) : ShikshaMitraAppService, IStudentGroupMemberAppService
{
	public async Task<StudentGroupMemberDto> AssignStudentToGroupAsync(AssignStudentToGroupDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId);
		var group = await studentGroupRepository.GetAsync(input.StudentGroupId);

		// Check if the student is already in this group
		var existingMembership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentId == student.Id && m.StudentGroupId == group.Id);

		// Get validation and action plan from domain service
		var result = await studentGroupManager.ValidateStudentGroupAssignmentAsync(
			student, group, input.IsLeader, existingMembership);

		// Execute the required operations based on validation results
		if (result.NeedsLeaderUpdate)
		{
			// Remove existing leader
			var currentLeader = await studentGroupMemberRepository.FirstOrDefaultAsync(
				m => m.StudentGroupId == group.Id && m.IsLeader);

			if (currentLeader != null)
			{
				currentLeader.RemoveAsLeader();
				await studentGroupMemberRepository.UpdateAsync(currentLeader);
			}
		}

		if (result.UpdateExistingMembership)
		{
			await studentGroupMemberRepository.UpdateAsync(result.ExistingMembership);
			return ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(result.ExistingMembership);
		}

		if (result.NeedsNewMembership)
		{
			await studentGroupMemberRepository.InsertAsync(result.NewMembership);
			return ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(result.NewMembership);
		}

		// Return the existing membership if no changes were needed
		return ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(existingMembership);
	}

	public async Task RemoveStudentFromGroupAsync(Guid studentId, Guid groupId)
	{
		// Direct repository operation - no domain logic needed
		var membership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentId == studentId && m.StudentGroupId == groupId);

		if (membership != null)
		{
			await studentGroupMemberRepository.DeleteAsync(membership);
		}
	}

	public async Task<StudentGroupMemberDto> SetGroupLeaderAsync(SetGroupLeaderDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId);
		var group = await studentGroupRepository.GetAsync(input.StudentGroupId);

		// First, remove any existing leader
		var currentLeader = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentGroupId == group.Id && m.IsLeader);

		if (currentLeader != null)
		{
			currentLeader.RemoveAsLeader();
			await studentGroupMemberRepository.UpdateAsync(currentLeader);
		}

		// Check if the student is a member of the group
		var membership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentId == input.StudentId && m.StudentGroupId == input.StudentGroupId);

		// Use domain service to prepare the membership update
		var updatedMembership = await studentGroupManager.PrepareLeadershipUpdateAsync(student, group, membership);

		if (membership != null)
		{
			// Update existing membership
			await studentGroupMemberRepository.UpdateAsync(updatedMembership);
		}
		else
		{
			// Insert new membership
			await studentGroupMemberRepository.InsertAsync(updatedMembership);
		}

		return ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(updatedMembership);
	}

	public async Task RemoveGroupLeaderAsync(Guid groupId)
	{
		var currentLeader = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentGroupId == groupId && m.IsLeader);

		if (currentLeader != null)
		{
			currentLeader.RemoveAsLeader();
			await studentGroupMemberRepository.UpdateAsync(currentLeader);
		}
	}

	public async Task<List<StudentDto>> GetGroupMembersAsync(Guid groupId)
	{
		var group = await studentGroupRepository.GetAsync(groupId);

		var memberIds = (await studentGroupMemberRepository
			.GetListAsync(m => m.StudentGroupId == group.Id))
			.ConvertAll(m => m.StudentId);

		var students = await studentRepository
			.GetListAsync(s => memberIds.Contains(s.Id));

		return ObjectMapper.Map<List<Student>, List<StudentDto>>(students);
	}

	public async Task<StudentDto> GetGroupLeaderAsync(Guid groupId)
	{
		var leaderMembership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentGroupId == groupId && m.IsLeader);

		if (leaderMembership == null)
		{
			return null;
		}

		var leader = await studentRepository.GetAsync(leaderMembership.StudentId);
		return ObjectMapper.Map<Student, StudentDto>(leader);
	}
}