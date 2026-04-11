using ShikshaMitra.Students.Enrollments.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentNotesAppService(
		IRepository<StudentNote, Guid> noteRepository,
		IRepository<Student, Guid> studentRepository,
		IIdentityUserRepository identityUserRepository
) : ApplicationService, IStudentNotesAppService
{
    public async Task<List<StudentNoteDto>> GetAllAsync(Guid studentId)
    {
        // Validate student exists
        var student = await studentRepository.FindAsync(studentId) ?? throw new UserFriendlyException($"Student not found: {studentId}");
		var notes = await noteRepository.GetListAsync(x => x.StudentId == studentId, includeDetails: true);

		var dtos = ObjectMapper.Map<List<StudentNote>, List<StudentNoteDto>>(notes.OrderByDescending(x => x.CreationTime).ToList());

		// Resolve creator names from CreatorId
		var creatorIds = notes
			.Where(n => n.CreatorId.HasValue)
			.Select(n => n.CreatorId!.Value)
			.Distinct()
			.ToList();

		if (creatorIds.Any())
		{
			var users = await identityUserRepository.GetListByIdsAsync(creatorIds);
			var userMap = users.ToDictionary(u => u.Id, u =>
				string.IsNullOrWhiteSpace(u.Name) && string.IsNullOrWhiteSpace(u.Surname)
					? u.UserName
					: $"{u.Name} {u.Surname}".Trim());

			foreach (var dto in dtos)
			{
				if (dto.CreatorId.HasValue && userMap.TryGetValue(dto.CreatorId.Value, out var name))
					dto.CreatorName = name;
			}
		}

        return dtos;
    }

    public async Task CreateAsync(Guid studentId, CreateUpdateStudentNoteDto input)
    {
        // Validate student exists
        var student = await studentRepository.FindAsync(studentId) ?? throw new UserFriendlyException($"Student not found: {studentId}");
		var note = new StudentNote(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            studentId,
            input.Note,
            input.NextReminderDate,
            input.Status
        );
        await noteRepository.InsertAsync(note, autoSave: true);
    }

    public async Task UpdateAsync(Guid id, CreateUpdateStudentNoteDto input)
    {
        var note = await noteRepository.GetAsync(id);
        // Only allow update if note exists
        note.Update(input.Note, input.NextReminderDate, input.Status);
        await noteRepository.UpdateAsync(note, autoSave: true);
    }

    public async Task DeleteAsync(Guid id)
    {
        await noteRepository.DeleteAsync(id);
    }
}
