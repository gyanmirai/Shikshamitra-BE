using ShikshaMitra.Common.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Setting;
public class GradeRangeDto
{
    public float MinMarks { get; set; }

    public float MaxMarks { get; set; }

	[JsonConverter(typeof(DescriptionEnumConverter<GradeType>))]
	public GradeType Grade { get; set; }

}
