using System.Security.AccessControl;
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>
/// Represents a learning resource for a subject in a standard, optionally tied to a specific chapter.
/// </summary>
public class LearningResource : FullAuditedAggregateRoot<Guid>
{
    #region Properties

    /// <summary>
    /// Gets or sets the ID of the standard (e.g., Grade 6).
    /// </summary>
    public Guid StandardId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the subject (e.g., Math, Science).
    /// </summary>
    public Guid SubjectId { get; set; }

    /// <summary>
    /// Optionally links this resource to a specific syllabus chapter.
    /// </summary>
    public Guid? ChapterId { get; set; }

    /// <summary>
    /// Gets or sets the title of the resource (e.g., "Electricity Overview Video").
    /// </summary>
    public string ResourceTitle { get; set; }

    /// <summary>
    /// Gets or sets a short description about the resource.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the file path or URL to the resource document.
    /// </summary>
    public string DocumentUrl { get; set; }

    /// <summary>
    /// Gets or sets the ID of the associated resource type (e.g., PDF, Video).
    /// </summary>
    public Guid ResourceTypeId { get; set; }

    /// <summary>
    /// Navigation property to the resource type.
    /// </summary>
    public ResourceType ResourceType { get; set; }

    // Optional: navigation property
    public SubjectChapter? Chapter { get; set; }

	#endregion

	#region Constructors
	protected LearningResource()
	{
		// This constructor is for EF Core
	}

	public LearningResource(
		Guid standardId,
		Guid subjectId,
		string resourceTitle,
		string documentUrl,
		Guid resourceTypeId,
		string? description = null,
		Guid? chapterId = null)
	{
		StandardId = standardId;
		SubjectId = subjectId;
		ResourceTitle = resourceTitle;
		DocumentUrl = documentUrl;
		ResourceTypeId = resourceTypeId;
		Description = description;
		ChapterId = chapterId;
	}

	#endregion

	#region Methods
	#endregion
}
