using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory.Dtos;

public class CreateExpenseCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}

public class UpdateExpenseCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}
