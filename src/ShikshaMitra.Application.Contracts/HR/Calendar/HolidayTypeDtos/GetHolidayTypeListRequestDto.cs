namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// DTO for querying HolidayType list with filtering and pagination
/// </summary>
public class GetHolidayTypeListRequestDto
{
    /// <summary>
    /// Search term for filtering by name
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Page number for pagination (1-based)
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Field to sort by (e.g., "name", "creationTime")
    /// </summary>
    public string? Sorting { get; set; }
}
