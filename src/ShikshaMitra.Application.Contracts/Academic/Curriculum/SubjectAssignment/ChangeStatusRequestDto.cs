using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class ChangeStatusRequestDto
{
    [Required]
    public int Status { get; set; }
}
