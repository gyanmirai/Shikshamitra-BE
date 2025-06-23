using System.Collections.Generic;
using Volo.Abp.Validation;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

public static class LibraryTimingValidator
{
	public static void Validate(List<CreateUpdateLibraryTimingDto> timings)
        {
        foreach(var timing in timings)
		{
			if (timing.OpeningTime >= timing.ClosingTime)
			{
				throw new AbpValidationException($"Opening time must be earlier than closing time for {timing.Day}.");
			}
		}
	}
}
