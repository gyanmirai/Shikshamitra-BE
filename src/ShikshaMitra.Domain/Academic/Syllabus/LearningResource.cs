using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Syllabus;

public class LearningResource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    #region Properties  
    public Guid? TenantId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public string ResourceTitle { get; set; }
    public string DocumentUrl { get; set; }
    public SubjectChapter? Chapter { get; set; }
    #endregion

    #region Constructors  
    protected LearningResource()
    {
    }

    public LearningResource(
        Guid id,
        Guid? tenantId,
        Guid standardId,
        Guid subjectId,
        string resourceTitle,
        Guid? chapterId = null)
    {
        Id = id;
        TenantId = tenantId;
        StandardId = standardId;
        SubjectId = subjectId;
        ResourceTitle = resourceTitle;
        ChapterId = chapterId;
    }
    #endregion

    #region Methods  
    #endregion
}
