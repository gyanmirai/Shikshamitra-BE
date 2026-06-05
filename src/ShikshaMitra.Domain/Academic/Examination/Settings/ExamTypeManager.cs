using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamTypeManager(
    IRepository<ExamType, Guid> examTypeRepository,
    IGuidGenerator guidGenerator,
    IDataFilter dataFilter
) : DomainService
{
    public async Task<ExamType> CreateAsync(string name, string? description, Guid academicMediumId)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        // Check including soft-deleted rows — the unique DB index does not filter on IsDeleted,
        // so re-creating a previously deleted name would hit a constraint violation without this.
        ExamType? existing;
        using (dataFilter.Disable<ISoftDelete>())
        {
            existing = await examTypeRepository.FirstOrDefaultAsync(x => x.Name == name && x.AcademicMediumId == academicMediumId);
        }

        if (existing != null && !existing.IsDeleted)
        {
            throw new UserFriendlyException($"Exam Type with name '{name}' already exists for this Academic Medium.");
        }

        if (existing != null && existing.IsDeleted)
        {
            // Restore the soft-deleted record rather than inserting a duplicate row.
            existing.IsDeleted = false;
            existing.DeletionTime = null;
            existing.DeleterId = null;
            existing.SetName(name);
            existing.SetDescription(description);
            return await examTypeRepository.UpdateAsync(existing);
        }

        var examType = new ExamType(guidGenerator.Create(), CurrentTenant.Id, name, description, academicMediumId);
        return await examTypeRepository.InsertAsync(examType);
    }

    public async Task UpdateAsync(ExamType examType, string name, string? description)
    {
        Check.NotNull(examType, nameof(examType));
        Check.NotNullOrWhiteSpace(name, nameof(name));

        if (examType.Name != name)
        {
            var existingExamType = await examTypeRepository.FirstOrDefaultAsync(x => x.Name == name && x.AcademicMediumId == examType.AcademicMediumId && x.Id != examType.Id);
            if (existingExamType != null)
            {
                throw new UserFriendlyException($"Exam Type with name '{name}' already exists for this Academic Medium.");
            }
            examType.SetName(name);
        }

        examType.SetDescription(description);
        await examTypeRepository.UpdateAsync(examType);
    }
}
