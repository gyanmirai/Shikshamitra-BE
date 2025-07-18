using AutoMapper;
using System;

namespace ShikshaMitra.Common;
public class AgeConverter : ITypeConverter<DateTimeOffset?, string>
{
	public string Convert(DateTimeOffset? source, string destination, ResolutionContext context)
	{
		if (!source.HasValue)
			return null;

		var today = DateTimeOffset.Now;
		var age = today - source.Value;
		var years = (int) (age.TotalDays / 365.25);
		var months = (int) ((age.TotalDays % 365.25) / 30.44);
		var days = (int) ((age.TotalDays % 365.25) % 30.44);
		return $"{years} years {months} months {days} days";
	}
}