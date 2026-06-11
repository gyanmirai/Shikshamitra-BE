using ShikshaMitra.Students.Enrollments.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentNotesAppService(
		IRepository<StudentNote, Guid> noteRepository,
		IStudentRepository studentRepository,
		IIdentityUserRepository identityUserRepository,
		StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IStudentNotesAppService
{
    public async Task<List<StudentNoteDto>> GetAllAsync(Guid studentId)
    {
        await GetScopedStudentAsync(studentId, nameof(GetAllAsync));
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
        await GetScopedStudentAsync(studentId, nameof(CreateAsync));
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
		await GetScopedStudentAsync(note.StudentId, nameof(UpdateAsync));
        // Only allow update if note exists
        note.Update(input.Note, input.NextReminderDate, input.Status);
        await noteRepository.UpdateAsync(note, autoSave: true);
    }

    public async Task DeleteAsync(Guid id)
    {
		var note = await noteRepository.GetAsync(id);
		await GetScopedStudentAsync(note.StudentId, nameof(DeleteAsync));
        await noteRepository.DeleteAsync(note, autoSave: true);
    }

	private async Task<Student> GetScopedStudentAsync(Guid studentId, string operation)
	{
		var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var student = await AsyncExecuter.FirstOrDefaultAsync(query.Where(student => student.Id == studentId))
		              ?? throw new EntityNotFoundException(typeof(Student), studentId);

		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, operation);
		return student;
	}
}
