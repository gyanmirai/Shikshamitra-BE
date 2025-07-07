using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Homework;
public class HomeworkAssignmentAppService(
	IRepository<HomeworkAssignmentEntity, Guid> repository,
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentSubmissionEntity, Guid> submissionRepository,
	IBlobContainer blobContainer
)
	: CrudAppService<
		HomeworkAssignmentEntity,
		HomeworkAssignmentDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateHomeworkAssignmentDto
	>(repository),
		IHomeworkAssignmentAppService
{
	public async Task<HomeworkAssignmentDto> PublishAsync(
		Guid id,
		PublishHomeworkAssignmentDto input
	)
	{
		var entity = await repository.GetAsync(id);

		if (input.IsPublished)
		{
			entity.Publish();
			await CreateSubmissionsForDivisionStudents(entity);
		}
		else
		{
			entity.Unpublish();
		}

		entity = await repository.UpdateAsync(entity, autoSave: true);

		return await MapToGetOutputDtoAsync(entity);
	}

	private async Task CreateSubmissionsForDivisionStudents(HomeworkAssignmentEntity homework)
	{
		// Get all active students in the division
		var students = await studentRepository.GetListAsync(s =>
			s.DivisionId == homework.DivisionId && !s.IsDeleted
		);

		// Create submissions in bulk for better performance
		var submissions = students
			.Select(student => new StudentSubmissionEntity
			{
				TenantId = homework.TenantId,
				HomeworkAssignmentId = homework.Id,
				StudentId = student.Id,
				Status = SubmissionStatus.NotSubmitted,
			})
			.ToList();

		foreach (var submission in submissions)
		{
			await submissionRepository.InsertAsync(submission);
		}
	}

	public async Task<HomeworkAssignmentDto> UnpublishAsync(Guid id)
	{
		return await PublishAsync(id, new PublishHomeworkAssignmentDto { IsPublished = false });
	}

	public async Task<string> UploadDocumentAsync(Guid id, DocumentDto file)
	{
		var entity = await Repository.GetAsync(id);

		// Remove old document if exists
		if (!string.IsNullOrEmpty(entity.DocumentUrl))
		{
			await DeleteDocumentAsync(entity.DocumentUrl);
		}

		// Save new document
		var documentUrl = await SaveDocumentAsync(file);
		entity.UpdateDocument(documentUrl);

		await Repository.UpdateAsync(entity, autoSave: true);

		return documentUrl;
	}

	public async Task RemoveDocumentAsync(Guid id)
	{
		var entity = await Repository.GetAsync(id);

		if (!string.IsNullOrEmpty(entity.DocumentUrl))
		{
			await DeleteDocumentAsync(entity.DocumentUrl);
			entity.UpdateDocument(null);
			await Repository.UpdateAsync(entity, autoSave: true);
		}
	}

	private async Task<string> SaveDocumentAsync(DocumentDto file)
	{
		var fileName = $"{GuidGenerator.Create()}{Path.GetExtension(file.FileName)}";

		await blobContainer.SaveAsync(fileName, file.Content, overrideExisting: true);

		return fileName;
	}

	private async Task DeleteDocumentAsync(string fileName)
	{
		if (await blobContainer.ExistsAsync(fileName))
		{
			await blobContainer.DeleteAsync(fileName);
		}
	}
}
