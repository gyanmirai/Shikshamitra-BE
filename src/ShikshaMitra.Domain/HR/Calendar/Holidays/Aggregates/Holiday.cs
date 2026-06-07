using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Calendar.Holidays;

/// <summary>
/// Represents a holiday/special day in the calendar.
/// Tenant-specific with audience targeting and file attachments support.
/// </summary>
public class Holiday : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Academic Year ID for filtering holidays by academic year (required)
    /// </summary>
    public Guid AcademicYearId { get; private set; }

    /// <summary>
    /// Name of the holiday (e.g., "Diwali", "New Year")
    /// </summary>
    public string HolidayName { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the EventType that defines color and category
    /// </summary>
    public Guid HolidayTypeId { get; private set; }

    /// <summary>
    /// Start date of the holiday
    /// </summary>
    public DateTimeOffset StartDate { get; private set; }

    /// <summary>
    /// End date of the holiday (inclusive)
    /// </summary>
    public DateTimeOffset EndDate { get; private set; }

    /// <summary>
    /// Optional description of the holiday
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of user/group IDs this holiday applies to
    /// </summary>
    public List<Guid> AudienceIds { get; set; } = new();

    /// <summary>
    /// Comma-separated list of attachment file paths/URLs
    /// </summary>
    public string? Attachments { get; set; }

    public HolidayStatus GetStatus(DateTimeOffset now)
    {
        if (now < StartDate)
            return HolidayStatus.Upcoming;
        if (now > EndDate)
            return HolidayStatus.Completed;
        return HolidayStatus.Ongoing;
    }

    protected Holiday() { }

    internal Holiday(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string holidayName,
        Guid holidayTypeId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? description = null,
        List<Guid>? audienceIds = null,
        string? attachments = null) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        HolidayName = holidayName ?? throw new ArgumentNullException(nameof(holidayName));
        HolidayTypeId = holidayTypeId;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
        AudienceIds = audienceIds ?? new();
        Attachments = attachments;

        ValidateDateRange();
    }

    /// <summary>
    /// Factory method to create new Holiday instance
    /// </summary>
    public static Holiday Create(
        Guid? tenantId,
        Guid academicYearId,
        string holidayName,
        Guid holidayTypeId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? description = null,
        List<Guid>? audienceIds = null,
        string? attachments = null)
    {
        return new Holiday(Guid.NewGuid(), tenantId, academicYearId, holidayName, holidayTypeId, startDate, endDate, description, audienceIds, attachments);
    }

    /// <summary>
    /// Update holiday details
    /// </summary>
    public void Update(
        string? holidayName = null,
        Guid? holidayTypeId = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        string? description = null,
        List<Guid>? audienceIds = null,
        string? attachments = null)
    {
        if (!string.IsNullOrWhiteSpace(holidayName))
            HolidayName = holidayName;

        if (holidayTypeId.HasValue)
            HolidayTypeId = holidayTypeId.Value;

        if (startDate.HasValue)
            StartDate = startDate.Value;

        if (endDate.HasValue)
            EndDate = endDate.Value;

        Description = description;

        if (audienceIds != null)
            AudienceIds = audienceIds;

        Attachments = attachments;

        ValidateDateRange();
    }

    /// <summary>
    /// Validate that start date is before or equal to end date
    /// </summary>
    private void ValidateDateRange()
    {
        if (StartDate > EndDate)
            throw new ArgumentException("Start date must be before or equal to end date", nameof(StartDate));
    }
}
