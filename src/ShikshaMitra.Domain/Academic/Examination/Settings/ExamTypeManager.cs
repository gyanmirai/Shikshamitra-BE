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
    public async Task<ExamType> CreateAsync(
        string name,
        string? description,
        Guid academicMediumId,
        string? category = null,
        SettingStatus status = SettingStatus.Active,
        double? defaultTotalMarks = null,
        double? defaultWeightage = null,
        bool includedInResult = false,
        bool marksEntryRequired = false,
        bool gradeScaleRequired = false,
        bool allowPracticalOral = false,
        Guid? defaultGradeScaleId = null
    )
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
            existing.SetCategory(category);
            existing.SetStatus(status);
            existing.SetDefaultTotalMarks(defaultTotalMarks);
            existing.SetDefaultWeightage(defaultWeightage);
            existing.SetIncludedInResult(includedInResult);
            existing.SetMarksEntryRequired(marksEntryRequired);
            existing.SetGradeScaleRequired(gradeScaleRequired);
            existing.SetAllowPracticalOral(allowPracticalOral);
            existing.SetDefaultGradeScaleId(defaultGradeScaleId);
            return await examTypeRepository.UpdateAsync(existing);
        }

        var examType = new ExamType(
            guidGenerator.Create(),
            CurrentTenant.Id,
            name,
            description,
            academicMediumId,
            category,
            status,
            defaultTotalMarks,
            defaultWeightage,
            includedInResult,
            marksEntryRequired,
            gradeScaleRequired,
            allowPracticalOral,
            defaultGradeScaleId
        );
        return await examTypeRepository.InsertAsync(examType);
    }

    public async Task UpdateAsync(
        ExamType examType,
        string name,
        string? description,
        string? category,
        SettingStatus status,
        double? defaultTotalMarks,
        double? defaultWeightage,
        bool includedInResult,
        bool marksEntryRequired,
        bool gradeScaleRequired,
        bool allowPracticalOral,
        Guid? defaultGradeScaleId
    )
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
        examType.SetCategory(category);
        examType.SetStatus(status);
        examType.SetDefaultTotalMarks(defaultTotalMarks);
        examType.SetDefaultWeightage(defaultWeightage);
        examType.SetIncludedInResult(includedInResult);
        examType.SetMarksEntryRequired(marksEntryRequired);
        examType.SetGradeScaleRequired(gradeScaleRequired);
        examType.SetAllowPracticalOral(allowPracticalOral);
        examType.SetDefaultGradeScaleId(defaultGradeScaleId);
        await examTypeRepository.UpdateAsync(examType);
    }
}
