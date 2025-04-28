using System;
using System.Linq;
using System.Threading.Tasks;
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
		var entity = await entranceExamRepository.GetAsync(id);

		await entranceExamManager.ValidateNoOverlappingExamAsync(input.EntranceExamSlotId, input.ExamDate, id);

		ObjectMapper.Map(input, entity);

		await entranceExamRepository.UpdateAsync(entity);

		return ObjectMapper.Map<EntranceExam, EntranceExamDto>(entity);
	}

	public async Task<EntranceExamDto> GetByStudentEnrollmentId(Guid studentEnrollmentId)
	{
		var entity = await entranceExamRepository.GetAsync(x => x.StudentEnrollmentId == studentEnrollmentId);
		return ObjectMapper.Map<EntranceExam, EntranceExamDto>(entity);
	}

	public async Task<SlotStudentCountDto> GetStudentCountForSlotByDateAsync(Guid slotId, DateTimeOffset examDate)
	{
		var queryable = await Repository.GetQueryableAsync();

		var dateOnly = examDate.Date;
		var query = queryable.Where(e =>
			e.EntranceExamSlotId == slotId &&
			e.ExamDate.Date == dateOnly);

		var count = await AsyncExecuter.CountAsync(query);

		return new SlotStudentCountDto
		{
			EntranceExamSlotId = slotId,
			ExamDate = examDate,
			StudentCount = count
		};
	}
}