using System;
namespace ShikshaMitra.Finance.Fees.Settings.Dtos;
public class FeeCategoryOptionDto
{
 public Guid Id { get; set; }
 public string Name { get; set; } = string.Empty;
 public bool IsActive { get; set; }
}
