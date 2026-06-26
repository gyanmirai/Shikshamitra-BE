using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.QuestionPapers;

public class QuestionPaperSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public bool RequireApprovedQuestions { get; set; } = true;
    public bool EnableBlueprintValidation { get; set; } = true;
    public bool EnableRecentQuestionBlock { get; set; }
    public int RecentQuestionBlockDays { get; set; } = 90;
    public decimal DistributionTolerancePercentage { get; set; } = 5;
    public bool EnablePdfCompilation { get; set; } = true;
    public bool LockAfterPublish { get; set; } = true;
    public bool EnableAiBlueprintGenerator { get; set; }
    public bool EnableAiPaperGenerator { get; set; }

    private QuestionPaperSetting() { }

    public QuestionPaperSetting(Guid id, Guid? tenantId, Guid academicYearId) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
    }
}
