using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>  
/// Represents a chapter in a subject for a specific standard.  
/// </summary>
public class SubjectChapter : FullAuditedAggregateRoot<Guid>
{
    #region Properties  

    /// <summary>  
    /// Gets or sets the ID of the standard (e.g., Grade 6, Grade 10).  
    /// </summary>  
    public Guid StandardId { get; set; }

    /// <summary>  
    /// Gets or sets the ID of the subject to which the chapter belongs (e.g., Science, Math).  
    /// </summary>  
    public Guid SubjectId { get; set; }

    /// <summary>  
    /// Gets or sets the chapter number (e.g., 1, 2, 3).  
    /// </summary>  
    public int ChapterNo { get; set; }

    /// <summary>  
    /// Gets or sets the name/title of the chapter.  
    /// </summary>  
    public string ChapterName { get; set; }

    /// <summary>  
    /// Gets or sets the planned start time of the chapter.  
    /// </summary>  
    public DateTimeOffset ChapterStartTime { get; set; }

    /// <summary>  
    /// Gets or sets the planned end time of the chapter.  
    /// </summary>  
    public DateTimeOffset ChapterEndTime { get; set; }

    /// <summary>  
    /// Gets or sets an optional description of the chapter (e.g., objectives, summary).  
    /// </summary>  
    public string? Description { get; set; }

    #endregion

    #region Constructors  

    // Default constructor  
    public SubjectChapter() { }

    // Parameterized constructor  
    public SubjectChapter(Guid standardId, Guid subjectId, int chapterNo, string chapterName, DateTimeOffset chapterStartTime, DateTimeOffset chapterEndTime, string? description = null)
    {
        StandardId = standardId;
        SubjectId = subjectId;
        ChapterNo = chapterNo;
        ChapterName = chapterName;
        ChapterStartTime = chapterStartTime;
        ChapterEndTime = chapterEndTime;
        Description = description;
    }

    #endregion

    #region Methods  
    #endregion
}
