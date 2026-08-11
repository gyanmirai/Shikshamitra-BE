using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records.Notes;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records.Notes;

[RemoteService(false)]
public class StudentNotesAppService(
	IRepository<StudentNote, Guid> noteRepository,
	IStudentRepository studentRepository,
	IIdentityUserRepository identityUserRepository,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService, IStudentNotesAppService
{
	public async Task<List<StudentNoteDto>> GetAllAsync(Guid studentId)
	{
		await GetScopedStudentAsync(studentId, nameof(GetAllAsync));
		var notes = await noteRepository.GetListAsync(x => x.StudentId == studentId, includeDetails: true);
		var orderedNotes = notes.OrderByDescending(x => x.CreationTime).ToList();
		var dtos = ObjectMapper.Map<List<StudentNote>, List<StudentNoteDto>>(orderedNotes);
		var creatorIds = notes
			.Where(note => note.CreatorId.HasValue)
			.Select(note => note.CreatorId!.Value)
			.Distinct()
			.ToList();

		if (creatorIds.Count == 0)
		{
			return dtos;
		}

		var users = await identityUserRepository.GetListByIdsAsync(creatorIds);
		var userMap = users.ToDictionary(
			user => user.Id,
			user => string.IsNullOrWhiteSpace(user.Name) && string.IsNullOrWhiteSpace(user.Surname)
				? user.UserName
				: $"{user.Name} {user.Surname}".Trim());

		foreach (var dto in dtos)
		{
			if (dto.CreatorId.HasValue && userMap.TryGetValue(dto.CreatorId.Value, out var name))
			{
				dto.CreatorName = name;
			}
		}

		return dtos;
	}

	public async Task CreateAsync(Guid studentId, CreateUpdateStudentNoteDto input)
	{
		await GetScopedStudentAsync(studentId, nameof(CreateAsync));
		var note = new StudentNote(
			GuidGenerator.Create(), CurrentTenant.Id, studentId, input.Note, input.NextReminderDate, input.Status);
		await noteRepository.InsertAsync(note, autoSave: true);
	}

	public async Task UpdateAsync(Guid id, CreateUpdateStudentNoteDto input)
	{
		var note = await noteRepository.GetAsync(id);
		await GetScopedStudentAsync(note.StudentId, nameof(UpdateAsync));
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
		var student = await AsyncExecuter.FirstOrDefaultAsync(query.Where(item => item.Id == studentId))
			?? throw new EntityNotFoundException(typeof(Student), studentId);
		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, operation);
		return student;
	}
}
