using Microsoft.AspNetCore.Http;
using ShikshaMitra.Common.Header;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAppService(ISubjectRepository subjectRepository, SubjectManager subjectManager, IHttpContextAccessor httpContextAccessor) : ApplicationService, ISubjectAppService
{
    public async Task<SubjectDto> CreateAsync(CreateSubjectDto createSubjectDto)
    {
        try
        {
            (Guid academicYearId, Guid academicBoardId, Guid academicMediumId) = HeaderValidationExtractionHelper.ValidateAndExtractHeaders(httpContextAccessor.HttpContext.Request.Headers);

			// Create a new Subject entity using the provided details and extracted headers
			Subject subject = subjectManager.CreateSubject(
			   academicYearId,
			   academicBoardId,
			   academicMediumId,
			   createSubjectDto.ClassId,
			   createSubjectDto.SubjectCode,
			   createSubjectDto.SubjectName,
			   createSubjectDto.ScheduledDate,
			   createSubjectDto.SubjectColor,
			   createSubjectDto.LearningGoals,
			   createSubjectDto.Prerequisites,
			   createSubjectDto.Description,
			   createSubjectDto.Status
			);

            // Add each teacher from the provided list to the subject
            foreach (var teacherDto in createSubjectDto.Teachers)
            {
				AddTeacherToSubject(subject, teacherDto);
			}

            // Save the newly created subject, along with its associated teachers, to the repository
            await subjectRepository.InsertAsync(subject);

            // Map the Subject entity to a SubjectDto and return it as the result
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

	private void AddTeacherToSubject(Subject subject, TeacherDto teacherDto)
	{
		// Create a new SubjectTeacher entity for the teacher  
		var teacher = new SubjectTeacher(
			GuidGenerator.Create(), // Generate a unique ID for the teacher  
			subject.Id,             // Associate the teacher with the subject  
			teacherDto.TeacherId,   // Assign the teacher's ID  
			teacherDto.TeacherType  // Specify the teacher's type  
		);

		// Add the teacher to the subject's collection of teachers  
		subject.AddTeacher(teacher);
	}
}
