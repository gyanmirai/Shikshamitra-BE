using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.IssueReturn;

public class IssueBookRequest
{
    [Required]
    public string AccessionNo { get; set; }

    [Required]
    public Guid MemberId { get; set; }

    [Required]
    public DateTime IssueDate { get; set; }

    [Required]
    public DateTime DueDate { get; set; }
}
