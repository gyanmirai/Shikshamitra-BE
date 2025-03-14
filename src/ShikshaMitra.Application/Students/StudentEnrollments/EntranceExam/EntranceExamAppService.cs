using AutoMapper.Internal.Mappers;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExamAppService(IEntranceExamRepository entranceExamRepository, EntranceExamManager entranceExamManager) :
	CrudAppService<EntranceExam, EntranceExamDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEntranceExamDto>(entranceExamRepository),
	IEntranceExamAppService
{
	public override async Task<EntranceExamDto> CreateAsync(CreateUpdateEntranceExamDto input)
	{
		await entranceExamManager.ValidateNoOverlappingExamAsync(input.EntranceExamSlotId, input.ExamDate);

		var entity = ObjectMapper.Map<CreateUpdateEntranceExamDto, EntranceExam>(input);

		await entranceExamRepository.InsertAsync(entity);

		return ObjectMapper.Map<EntranceExam, EntranceExamDto>(entity);
	}

	public override async Task<EntranceExamDto> UpdateAsync(Guid id, CreateUpdateEntranceExamDto input)
	{
		// Retrieve the existing entity.
		var entity = await entranceExamRepository.GetAsync(id);

		await entranceExamManager.ValidateNoOverlappingExamAsync(input.EntranceExamSlotId, input.ExamDate, id);

		// Map the updated fields from input DTO to the entity.
		ObjectMapper.Map(input, entity);

		// Update the entity in the repository.
		await entranceExamRepository.UpdateAsync(entity);

		// Return the updated entity mapped to a DTO.
		return ObjectMapper.Map<EntranceExam, EntranceExamDto>(entity);
	}

	// get Student Entrance Exam Dto based on Student Enrollment Id
	//[RemoteService(false)]
	public async Task<EntranceExamDto> GetByStudentEnrollmentId(Guid studentEnrollmentId)
	{
		var entity = await entranceExamRepository.GetAsync(x => x.StudentEnrollmentId == studentEnrollmentId);
		return ObjectMapper.Map<EntranceExam, EntranceExamDto>(entity);
	}
}