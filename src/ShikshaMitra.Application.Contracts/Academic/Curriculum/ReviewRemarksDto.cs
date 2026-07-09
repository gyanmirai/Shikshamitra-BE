using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class ReviewRemarksDto
{
    [MaxLength(1024)]
    public string? Comment { get; set; }
}
