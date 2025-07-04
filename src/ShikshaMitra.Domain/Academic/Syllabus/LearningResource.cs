using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Syllabus;

public class LearningResource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	public Guid? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the standard (e.g., Grade 1, Grade 2).
	/// </summary>
	public Guid StandardId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the subject (e.g., Math, Science).
	/// </summary>
	public Guid SubjectId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the chapter this resource belongs to. Can be null if the resource is for the whole subject/standard.
	/// </summary>
	public Guid? ChapterId { get; set; }

	/// <summary>
	/// Gets or sets the title of the learning resource.
	/// </summary>
	public string ResourceTitle { get; set; }

	/// <summary>
	/// Gets or sets the URL or path to the document/resource.
	/// </summary>
	public string DocumentUrl { get; set; }

	/// <summary>
	/// Navigation property to the associated chapter.
	/// </summary>
	public SubjectChapter? Chapter { get; set; }
	#endregion

	#region Constructors
	protected LearningResource() { }

	public LearningResource(
		Guid id,
		Guid? tenantId,
		Guid standardId,
		Guid subjectId,
		string resourceTitle,
		Guid? chapterId = null
	)
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
