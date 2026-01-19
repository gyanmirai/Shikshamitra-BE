using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamTypeManager(
	IRepository<ExamType, Guid> examTypeRepository,
	IGuidGenerator guidGenerator
) : DomainService
{
    public async Task<ExamType> CreateAsync(string name, string? description, Guid academicMediumId)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        var existingExamType = await examTypeRepository.FirstOrDefaultAsync(x => x.Name == name && x.AcademicMediumId == academicMediumId);
        if (existingExamType != null)
        {
            throw new UserFriendlyException($"Exam Type with name '{name}' already exists for this Academic Medium.");
        }

        var examType = new ExamType(guidGenerator.Create(), CurrentTenant.Id, name, description, academicMediumId);
        return await examTypeRepository.InsertAsync(examType);
    }
}
