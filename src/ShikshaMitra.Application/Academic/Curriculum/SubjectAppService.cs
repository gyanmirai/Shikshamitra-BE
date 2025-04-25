using Microsoft.AspNetCore.Http;
using ShikshaMitra.Common.Header;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAppService(ISubjectRepository subjectRepository, SubjectManager subjectManager, IHttpContextAccessor httpContextAccessor) : ApplicationService, ISubjectAppService
{
    public async Task<SubjectDto> CreateAsync(CreateSubjectDto subjectDto)
    {
        try
        {
			(Guid academicYearId, Guid academicBoardId, Guid academicMediumId) = HeaderValidationExtractionHelper.ValidateAndExtractHeaders(httpContextAccessor.HttpContext.Request.Headers);

			Subject subject = subjectManager.CreateSubject(
				academicYearId,
				academicBoardId,
				academicMediumId,
				subjectDto.ClassId,
                subjectDto.SubjectCode,
                subjectDto.SubjectName,
                subjectDto.PrimaryTeacherId,
                subjectDto.SecondaryTeacherIds ?? [],
                subjectDto.ScheduledDate,
                subjectDto.SubjectColor,
                subjectDto.LearningGoals,
                subjectDto.Prerequisites,
                subjectDto.Description
            );

            await subjectRepository.InsertAsync(subject);

			return ObjectMapper.Map<Subject, SubjectDto>(subject);
		}
        catch (Exception ex)
        {
            // Log the exception or handle it as needed
            throw new ApplicationException("An error occurred while creating the subject.", ex);
        }
    }

	// create a method to get all subjects for a class, use paged result
	public async Task<PagedResultDto<SubjectDto>> GetAllAsync(Guid classId, int skipCount = 0, int maxResultCount = 10)
	{
		try
		{
			// fetch all subjects for a class
			var subjects = await subjectRepository.GetSubjectsByClassAsync(
				classId,
				skipCount,
				maxResultCount
			);

			// map pagedresult to pagedresultdto for subjects
			return ObjectMapper.Map<PagedResultDto<Subject>, PagedResultDto<SubjectDto>>(subjects);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving subjects.", ex);
		}
	}
}
