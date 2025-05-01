using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

public class TempSubjectAppService(ISubjectRepository subjectRepository, IRepository<SubjectTeacher> subjectTeacherRepository, TempSubjectManager subjectManager) : ApplicationService, ITempSubjectAppService
{
    public async Task<TempSubjectDto> CreateAsync(CreateTempSubjectDto createSubjectDto)
    {
        try
        {
			// Extract headers from the current context
			var academicYearId = Guid.NewGuid();
			var academicBoardId = Guid.NewGuid();
			var academicMediumId = Guid.NewGuid();
			// Create a new Subject entity using the provided details and extracted headers
			TempSubject subject = subjectManager.CreateSubject(
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
            return ObjectMapper.Map<TempSubject, TempSubjectDto>(subject);
		}
        catch (Exception ex)
        {
            // Log the exception or handle it as needed
            throw new ApplicationException("An error occurred while creating the subject.", ex);
        }
    }

	public async Task<PagedResultDto<TempSubjectDto>> GetSubjectsByClassAsync(Guid classId, int skipCount = 0, int maxResultCount = 10)
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
			return ObjectMapper.Map<PagedResultDto<TempSubject>, PagedResultDto<TempSubjectDto>>(subjects);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving subjects.", ex);
		}
	}

    public async Task DeleteAsync(Guid id)
    {
        try
        {
            // Fetch the subject by ID  
            var subject = await subjectRepository.GetAsync(id);

            // Fetch all SubjectTeacher entities associated with the subject ID  
            var subjectTeachers = await subjectTeacherRepository.GetListAsync(x => x.SubjectId == id);

            // Delete all associated SubjectTeacher entities  
            await subjectTeacherRepository.DeleteManyAsync(subjectTeachers);

            // Delete the subject from the repository  
            await subjectRepository.DeleteAsync(subject);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while deleting the subject.", ex);
        }
    }

	public async Task ChangeSubjectStatusAsync(Guid subjectId, SubjectStatus status)
    {
        try
        {
            // Fetch the subject by ID  
            var subject = await subjectRepository.GetAsync(subjectId);

            // Check if the subject already has the desired status  
            if (subject.Status == status)
            {
                return; // No update needed  
            }

            // Change the status of the subject  
            subject.ChangeStatus(status);

            // Update the subject in the repository  
            await subjectRepository.UpdateAsync(subject);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while changing the subject status.", ex);
        }
    }

	private void AddTeacherToSubject(TempSubject subject, TeacherDto teacherDto)
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
