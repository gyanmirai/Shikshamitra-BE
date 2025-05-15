using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class SubjectAppService(IRepository<Subject, Guid> subjectRepository, SubjectManager subjectManager) : ApplicationService, ISubjectAppService
{
	public async Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto)
	{
		Subject subject = await subjectManager.CreateSubject(
			createSubjectDto.Name,
			createSubjectDto.MediumId,
			createSubjectDto.Color
		);

		await subjectRepository.InsertAsync(subject);
		
		return ObjectMapper.Map<Subject, SubjectDto>(subject);
	}

	public async Task DeleteAsync(Guid id)
	{
		var subject = await subjectRepository.GetAsync(id);
		await subjectRepository.DeleteAsync(id);
	}

	public async Task<List<SubjectDto>> GetListAsync(Guid mediumId)
	{
		List<Subject> subjects = await subjectRepository.GetListAsync(m => m.MediumId == mediumId);

		return ObjectMapper.Map<List<Subject>, List<SubjectDto>>(subjects);
	}

	public async Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto)
	{
		var subject = await subjectRepository.GetAsync(id);

		await subjectManager.UpdateAsync(subject, updateSubjectDto.Name, updateSubjectDto.MediumId, updateSubjectDto.Color);

		await subjectRepository.UpdateAsync(subject);

		return ObjectMapper.Map<Subject, SubjectDto>(subject);
	}
}
