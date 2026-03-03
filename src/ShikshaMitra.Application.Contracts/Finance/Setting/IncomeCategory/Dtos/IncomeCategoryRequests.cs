using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory.Dtos;

public class CreateIncomeCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}

public class UpdateIncomeCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}
