using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Setting;
public class GradeScaleDto : EntityDto<Guid>
{
	public string Title { get; set; }
	public int TotalMarks { get; set; }
	public List<GradeRangeDto> GradeRanges { get; set; } = new List<GradeRangeDto>();
}
