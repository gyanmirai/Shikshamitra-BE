using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.HR.Calendar;

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

    public string HolidayName { get; set; } = string.Empty;

    /// <summary>
    /// Holiday type ID
    /// </summary>
    public Guid HolidayTypeId { get; set; }

    /// <summary>
    /// Full HolidayType object with name, color, and description
    /// </summary>
    public HolidayTypeDto? HolidayType { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Description { get; set; }
    public List<Guid> AudienceIds { get; set; } = new();
    public string? Attachments { get; set; }
    public int Status { get; set; }
}
