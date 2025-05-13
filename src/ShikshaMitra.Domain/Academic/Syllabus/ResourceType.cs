using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>  
/// Represents the type of a learning resource (e.g., Video, PDF).  
/// </summary>  
public class ResourceType : FullAuditedAggregateRoot<Guid>
{
    #region Properties  
    /// <summary>  
    /// Gets or sets the name/type of the resource (e.g., Video, PDF).  
    /// </summary>  
    public string Type { get; set; }

    // Optional: Add a description or display order  
    /// <summary>  
    /// Gets or sets a description of the resource type.  
    /// </summary>  
    public string? Description { get; set; }
    #endregion

    #region Constructors  
	private ResourceType() { } // Required for EF Core

	public ResourceType(string type)
    {
        Type = type;
    }
    #endregion

    #region Methods   
    #endregion
}
