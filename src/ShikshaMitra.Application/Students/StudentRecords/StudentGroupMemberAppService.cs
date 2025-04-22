using System;
using System.Collections.Generic;
using System.Linq;
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
			student, group, input.IsLeader, input.IsDivisionLeader, existingMembership);

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
		
		if (result.NeedsDivisionLeaderUpdate)
		{
			// Remove existing division leader
			var currentDivisionLeader = await studentGroupMemberRepository.FirstOrDefaultAsync(
				m => m.StudentGroupId == group.Id && m.IsDivisionLeader);

			if (currentDivisionLeader != null)
			{
				currentDivisionLeader.RemoveAsDivisionLeader();
				await studentGroupMemberRepository.UpdateAsync(currentDivisionLeader);
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
	
	public async Task<StudentGroupMemberDto> SetDivisionLeaderAsync(SetDivisionLeaderDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId);
		var group = await studentGroupRepository.GetAsync(input.StudentGroupId);

		// First, remove any existing division leader
		var currentDivisionLeader = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentGroupId == group.Id && m.IsDivisionLeader);

		if (currentDivisionLeader != null)
		{
			currentDivisionLeader.RemoveAsDivisionLeader();
			await studentGroupMemberRepository.UpdateAsync(currentDivisionLeader);
		}

		// Check if the student is a member of the group
		var membership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentId == input.StudentId && m.StudentGroupId == input.StudentGroupId);

		// Use domain service to prepare the membership update
		var updatedMembership = await studentGroupManager.PrepareDivisionLeadershipUpdateAsync(student, group, membership);

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
	
	public async Task RemoveDivisionLeaderAsync(Guid groupId)
	{
		var currentDivisionLeader = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentGroupId == groupId && m.IsDivisionLeader);

		if (currentDivisionLeader != null)
		{
			currentDivisionLeader.RemoveAsDivisionLeader();
			await studentGroupMemberRepository.UpdateAsync(currentDivisionLeader);
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
	
	public async Task<StudentDto> GetDivisionLeaderAsync(Guid groupId)
	{
		var divisionLeaderMembership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentGroupId == groupId && m.IsDivisionLeader);

		if (divisionLeaderMembership == null)
		{
			return null;
		}

		var divisionLeader = await studentRepository.GetAsync(divisionLeaderMembership.StudentId);
		return ObjectMapper.Map<Student, StudentDto>(divisionLeader);
	}

	public async Task<List<StudentGroupMemberDto>> BulkAssignStudentsToGroupAsync(BulkAssignStudentsToGroupDto input)
	{
		var group = await studentGroupRepository.GetAsync(input.StudentGroupId);
		var studentIds = input.StudentIds.Distinct().ToList();
		var students = await studentRepository.GetListAsync(s => studentIds.Contains(s.Id));
		
		// Get all existing memberships for these students in this group to avoid duplicates
		var existingMemberships = await studentGroupMemberRepository.GetListAsync(
			m => m.StudentGroupId == group.Id && studentIds.Contains(m.StudentId));
		
		var results = new List<StudentGroupMemberDto>();
		
		foreach (var student in students)
		{
			// Check if the student is already in this group
			var existingMembership = existingMemberships.FirstOrDefault(m => m.StudentId == student.Id);
			
			// Skip students already in the group
			if (existingMembership != null)
			{
				results.Add(ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(existingMembership));
				continue;
			}
			
			// Use domain service to validate and create new membership
			var result = await studentGroupManager.ValidateStudentGroupAssignmentAsync(
				student, group, false, false, null); // Always adding as regular member, not leader or division leader
				
			if (result.NeedsNewMembership)
			{
				await studentGroupMemberRepository.InsertAsync(result.NewMembership);
				results.Add(ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(result.NewMembership));
			}
		}
		
		return results;
	}
}