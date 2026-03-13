using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Members;

[Authorize]
[RemoteService(false)]
public class LibraryMemberAppService(
	IRepository<LibraryMember, Guid> libraryMemberRepository,
	IRepository<Student, Guid> studentRepository,
	LibraryMemberManager libraryMemberManager,
	IGuidGenerator guidGenerator
) : ApplicationService, ILibraryMemberAppService
{
	public async Task CreateLibraryMember(Guid studentId, bool isActive = true)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new EntityNotFoundException($"Student with id: {studentId} not found");

		var memberIdentifier = libraryMemberManager.GetMemberIdentifier(student);

		var libraryMember = new LibraryMember(
			guidGenerator.Create(),
			CurrentTenant.Id,
			student.Id,
			memberIdentifier,
			isActive
		);

		await libraryMemberRepository.InsertAsync( libraryMember );
	}

	public async Task HandleLibraryMemberAsync(Guid studentId, bool isActive = true)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new EntityNotFoundException($"Student with id: {studentId} not found");

		// check if library member is already exists
		var libraryMember = await libraryMemberRepository.FindAsync(x => x.StudentId == studentId);
		if (libraryMember != null)
		{
			libraryMember.ChangeMemberStatus(isActive);
			await libraryMemberRepository.UpdateAsync( libraryMember );
		}
		else
		{
			await CreateLibraryMember(studentId, isActive);
		}
	}
}
