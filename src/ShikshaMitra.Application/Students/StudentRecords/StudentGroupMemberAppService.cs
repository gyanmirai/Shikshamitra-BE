using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students.StudentRecords;

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

		// Check if the student is already in ANY group (not just this one)
		var existingMembership = await studentGroupMemberRepository.FirstOrDefaultAsync(
			m => m.StudentId == student.Id);

		// If student belongs to a different group, remove them from that group first
		if (existingMembership != null && existingMembership.StudentGroupId != group.Id)
		{
			await studentGroupMemberRepository.DeleteAsync(existingMembership);
			// Reset existingMembership to null since we're removing them from the previous group
			existingMembership = null;
		}

		// Now check if they're in the target group
		if (existingMembership == null)
		{
			existingMembership = await studentGroupMemberRepository.FirstOrDefaultAsync(
				m => m.StudentId == student.Id && m.StudentGroupId == group.Id);
		}

		// Get validation and action plan from domain service
		var result = studentGroupManager.ValidateStudentGroupAssignmentAsync(
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
		var updatedMembership =  studentGroupManager.PrepareLeadershipUpdateAsync(student, group, membership);

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
		var updatedMembership = studentGroupManager.PrepareDivisionLeadershipUpdateAsync(student, group, membership);

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
		
		// Get ALL existing memberships for these students (not just in this group)
		// to ensure students can only belong to one group
		var allExistingMemberships = await studentGroupMemberRepository.GetListAsync(
			m => studentIds.Contains(m.StudentId));
		
		// Identify students already in this group vs. students in other groups
		var studentsInThisGroup = allExistingMemberships
			.Where(m => m.StudentGroupId == group.Id)
			.Select(m => m.StudentId)
			.ToHashSet();
		
		var studentsInOtherGroups = allExistingMemberships
			.Where(m => m.StudentGroupId != group.Id)
			.ToList();
		
		var results = new List<StudentGroupMemberDto>();
		
		// First, remove students from other groups
		foreach (var membership in studentsInOtherGroups)
		{
			await studentGroupMemberRepository.DeleteAsync(membership);
		}
		
		foreach (var student in students)
		{
			// Skip if student is already in this group
			if (studentsInThisGroup.Contains(student.Id))
			{
				var existingMembership = allExistingMemberships.First(m => m.StudentId == student.Id);
				results.Add(ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(existingMembership));
				continue;
			}
			
			// Create new membership for this student
			var result =  studentGroupManager.ValidateStudentGroupAssignmentAsync(
				student, group, false, false, null); // Always adding as regular member, not leader or division leader
				
			if (result.NeedsNewMembership)
			{
				await studentGroupMemberRepository.InsertAsync(result.NewMembership);
				results.Add(ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(result.NewMembership));
			}
		}
		
		return results;
	}

    public async Task<List<StudentGroupMemberDto>> AutoAssignStudentsToGroupsAsync(AutoAssignStudentsToGroupsDto input)
    {
        // Validate input
        if (input.GroupIds == null || input.GroupIds.Count == 0)
        {
            throw new UserFriendlyException("No groups provided for assignment.");
        }

        // Get all the requested groups
        var groups = await studentGroupRepository.GetListAsync(g => input.GroupIds.Contains(g.Id));
        
        if (groups.Count == 0)
        {
            throw new UserFriendlyException("No valid groups found for assignment.");
        }

        // Fetch students by EducationMediumId and optionally by DivisionId
        var studentsQuery = await studentRepository.GetQueryableAsync();
        studentsQuery = studentsQuery.Where(s => s.EducationMediumId == input.EducationMediumId);
        
        // Filter by division if specified
        if (input.DivisionId.HasValue)
        {
            studentsQuery = studentsQuery.Where(s => s.DivisionId == input.DivisionId);
        }

        var students = await AsyncExecuter.ToListAsync(studentsQuery);
        
        if (students.Count == 0)
        {
            throw new UserFriendlyException("No students found for the specified education medium" +
                (input.DivisionId.HasValue ? " and division." : "."));
        }

        // Get existing group memberships for all students
        var studentIds = students.Select(s => s.Id).ToList();
        var existingMemberships = await studentGroupMemberRepository.GetListAsync(
            m => studentIds.Contains(m.StudentId));

        // Delete any existing memberships for these students 
        // to ensure clean assignment
        foreach (var membership in existingMemberships)
        {
            await studentGroupMemberRepository.DeleteAsync(membership);
        }

        // Use the domain service to calculate optimal group assignments
        var groupAssignments = studentGroupManager.CalculateOptimalGroupAssignments(students, groups);

        var results = new List<StudentGroupMemberDto>();

        // Create new memberships based on the calculated assignments
        foreach (var assignment in groupAssignments)
        {
            var student = assignment.Key;
            var group = assignment.Value;
            
            // Create the group membership
            var membershipResult =  studentGroupManager.ValidateStudentGroupAssignmentAsync(
                student, group, false, false);
                
            if (membershipResult.NeedsNewMembership && membershipResult.NewMembership != null)
            {
                // Insert the new membership
                var newMembership = await studentGroupMemberRepository.InsertAsync(membershipResult.NewMembership);
                results.Add(ObjectMapper.Map<StudentGroupMember, StudentGroupMemberDto>(newMembership));
            }
        }

        return results;
    }
}