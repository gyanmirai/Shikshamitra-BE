using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.TransactionCategory.Dtos;

public class CreateTransactionCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}

public class UpdateTransactionCategoryRequest
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}
