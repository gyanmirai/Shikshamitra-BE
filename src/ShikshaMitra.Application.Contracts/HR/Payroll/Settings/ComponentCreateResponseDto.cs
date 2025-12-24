using System;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Response for component creation as per OpenAPI contract.
/// </summary>
public class ComponentCreateResponseDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
