using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Examination.Setting;
public class CreateGradeScaleDto
{
	[MaxLength(256)]
	public string Title { get; set; }
	[Range(1, int.MaxValue)]
	public int TotalMarks { get; set; }
	public Guid EducationMediumId { get; set; }
	public List<GradeRangeDto> GradeRanges { get; set; } = new List<GradeRangeDto>();
}
