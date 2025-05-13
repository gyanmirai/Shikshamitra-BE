using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    #region Properties
    public virtual Guid? TenantId { get; private set; }

    /// <summary>
    /// Identifier for the academic medium associated with the subject.
    /// </summary>
    public Guid AcademicMediumId { get; set; }

    /// <summary>
    /// Identifier for the standard associated with the subject.
    /// </summary>
    public Guid StandardId { get; set; }

    /// <summary>
    /// Navigation property to the Standard entity associated with the subject.
    /// </summary>
    [ForeignKey(nameof(StandardId))]
    public Standard Standard { get; set; }

    /// <summary>
    /// Unique code representing the subject.
    /// </summary>
    public string SubjectCode { get; set; }

    /// <summary>
    /// Name of the subject.
    /// </summary>
    public string SubjectName { get; set; }

    /// <summary>
    /// Collection of teachers assigned to the subject.
    /// </summary>
    public ICollection<SubjectTeacher> Teachers { get; set; } = new HashSet<SubjectTeacher>();

    /// <summary>
    /// Scheduled date for the subject.
    /// </summary>
    public DateTimeOffset ScheduledDate { get; set; }

    /// <summary>
    /// Color associated with the subject for identification or categorization.
    /// </summary>
    public string SubjectColor { get; set; }

    /// <summary>
    /// Learning goals or objectives for the subject.
    /// </summary>
    public string? LearningGoals { get; set; }

    /// <summary>
    /// Prerequisites required for understanding the subject.
    /// </summary>
    public string? Prerequisites { get; set; }

    /// <summary>
    /// Description providing additional details about the subject.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Path or reference to the uploaded document related to the subject.
    /// </summary>
    public string? UploadedDocument { get; set; }

    /// <summary>
    /// Current status of the subject (e.g., Draft, Published).
    /// </summary>
    public SubjectStatus Status { get; set; } = SubjectStatus.Draft;
    #endregion

    #region Constructors
    /// <summary>
    /// Private constructor for ORM and deserialization purposes.
    /// </summary>
    private StandardSubjectAssignment()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StandardSubjectAssignment"/> class with specified parameters.
    /// </summary>
    public StandardSubjectAssignment(
        Guid id,
        Guid? tenantId,
        Guid academicMediumId,
        Guid standardId,
        string subjectCode,
        string subjectName,
        DateTimeOffset scheduledDate,
        string subjectColor,
        string? learningGoals,
        string? prerequisites,
        string? description,
        SubjectStatus status)
        : base(id)
    {
		Check.NotNullOrEmpty(subjectCode, nameof(SubjectCode));
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
        StandardId = standardId;
        SubjectCode = Check.NotNullOrEmpty(subjectCode, nameof(SubjectCode));
        SubjectName = Check.NotNullOrEmpty(subjectName, nameof(SubjectName));
        ScheduledDate = scheduledDate;
        SubjectColor = subjectColor;
        LearningGoals = learningGoals;
        Prerequisites = prerequisites;
        Description = description;
        Status = status;
    }
    #endregion

    #region Methods  
    /// <summary>
    /// Adds a teacher to the subject.
    /// </summary>
    /// <param name="teacher">The teacher to add.</param>
    public void AddTeacher(SubjectTeacher teacher)
    {
        if (teacher == null) throw new ArgumentNullException(nameof(teacher));
        Teachers.Add(teacher);
    }

    /// <summary>
    /// Removes a teacher from the subject.
    /// </summary>
    /// <param name="teacher">The teacher to remove.</param>
    public void RemoveTeacher(SubjectTeacher teacher)
    {
        if (teacher == null) throw new ArgumentNullException(nameof(teacher));
        Teachers.Remove(teacher);
    }

    /// <summary>
    /// Changes the status of the subject.
    /// </summary>
    /// <param name="status">The new status to set.</param>
    public void ChangeStatus(SubjectStatus status)
    {
        Status = status;
    }
    #endregion
}
