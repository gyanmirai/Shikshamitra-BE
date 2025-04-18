using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAppService(ISubjectRepository subjectRepository, SubjectManager subjectManager, IRepository<Subject> subjectnewRepository) : ApplicationService, ISubjectAppService
{
    public async Task CreateAsync(CreateSubjectDto subjectDto)
    {
        try
        {
			Subject subject = subjectManager.CreateSubject(
					subjectDto.ClassId,
					subjectDto.SubjectCode,
					subjectDto.SubjectName,
					subjectDto.PrimaryTeacherId,
					subjectDto.ScheduledDate,
					subjectDto.SubjectColor,
					subjectDto.LearningGoals,
					subjectDto.Prerequisites,
					subjectDto.Description
				);

			await subjectRepository.InsertAsync(subject);
		}
        catch (Exception ex)
        {
            // Log the exception or handle it as needed
            throw new ApplicationException("An error occurred while creating the subject.", ex);
        }
    }
}
