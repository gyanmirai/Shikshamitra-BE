using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory.Dtos;

public class CreateChargeCategoryRequest
{
    [Required]
    public string Name { get; set; }

    [Required]
    public bool IsApprovalRequired { get; set; }

    public string? Description { get; set; }
}

public class UpdateChargeCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public bool? IsApprovalRequired { get; set; } // Marked optional in docs example but could be treated as update-if-present or required logic. Let's make it nullable to support partial updates if needed, though docs imply full object. Actually docs say required: false for Update.

    public string? Description { get; set; }
}
