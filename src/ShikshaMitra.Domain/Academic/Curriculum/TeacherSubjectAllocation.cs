using System.Collections.Generic;
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>  
/// Represents the allocation of teachers to a specific subject within a standard and division.  
/// </summary>  
public class TeacherSubjectAllocation : FullAuditedAggregateRoot<Guid>
{
    #region Properties  

    /// <summary>  
    /// Gets or sets the ID of the standard (e.g., Grade 1, Grade 2).  
    /// </summary>  
    public Guid StandardId { get; set; }

    /// <summary>  
    /// Gets or sets the ID of the division (e.g., Division A, B).  
    /// </summary>  
    public Guid DivisionId { get; set; }

    /// <summary>  
    /// Gets or sets the ID of the subject (e.g., Math, Science).  
    /// </summary>  
    public Guid SubjectId { get; set; }

    /// <summary>  
    /// List of teachers assigned to this subject within the given standard and division.  
    /// </summary>  
    public ICollection<AllocatedTeacher> Teachers { get; set; }

    #endregion

    #region Constructors  

    /// <summary>  
    /// Initializes a new instance of the <see cref="TeacherSubjectAllocation"/> class.  
    /// </summary>  
    public TeacherSubjectAllocation()
    {
        Teachers = new List<AllocatedTeacher>();
    }

    #endregion

    #region Methods  
    #endregion
}
