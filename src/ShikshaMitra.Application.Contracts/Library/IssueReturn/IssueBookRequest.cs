using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.IssueReturn;

public class IssueBookRequest
{
    [Required]
    public string AccessionNo { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    [Required]
    public Guid MemberId { get; set; }

    [Required]
    public DateTime IssueDate { get; set; }

    [Required]
    public DateTime DueDate { get; set; }
}
