using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>  
/// Represents a teacher assigned to a specific subject allocation.  
/// </summary>  
public class AllocatedTeacher : FullAuditedEntity
{
    #region Properties  

    /// <summary>  
    /// Gets or sets the ID of the teacher subject allocation this teacher is linked to.  
    /// </summary>  
    public Guid TeacherSubjectAllocationId { get; set; }

    /// <summary>  
    /// Gets or sets the ID of the teacher.  
    /// </summary>  
    public Guid TeacherId { get; set; }

    /// <summary>  
    /// Navigation property to the associated TeacherSubjectAllocation.  
    /// </summary>  
    public TeacherSubjectAllocation Allocation { get; set; }

    #endregion

    #region Constructors  

    /// <summary>  
    /// Initializes a new instance of the <see cref="AllocatedTeacher"/> class.  
    /// </summary>  
    /// <param name="teacherSubjectAllocationId">The ID of the subject allocation.</param>  
    /// <param name="teacherId">The ID of the teacher.</param>  
    public AllocatedTeacher(Guid teacherSubjectAllocationId, Guid teacherId)
    {
        TeacherSubjectAllocationId = teacherSubjectAllocationId;
        TeacherId = teacherId;
    }

    #endregion

    #region Methods  

    public override object?[] GetKeys()
    {
        return new object?[] { TeacherSubjectAllocationId, TeacherId };
    }

    #endregion
}
