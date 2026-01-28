using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;

/// <summary>
/// Data Transfer Object for reading Holiday data
/// </summary>
public class HolidayDto : AuditedEntityDto<Guid>
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Academic Year ID for filtering holidays by academic year
    /// </summary>
    public Guid AcademicYearId { get; set; }

    public string HolidayName { get; set; }
    public Guid HolidayTypeId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? Description { get; set; }
    public List<Guid> AudienceIds { get; set; } = new();
    public string? Attachments { get; set; }
    public int Status { get; set; }
}
