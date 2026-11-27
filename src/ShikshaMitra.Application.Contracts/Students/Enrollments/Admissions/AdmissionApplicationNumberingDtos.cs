using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class SaveAdmissionApplicationNumberingDto
{
	[Range(0, int.MaxValue)] public int ExpectedVersion { get; set; }
	[Required(AllowEmptyStrings = true), MaxLength(20)] public string Prefix { get; set; } = "APP";
	[Required(AllowEmptyStrings = true), MaxLength(1)] public string Separator { get; set; } = "-";
	[Required(AllowEmptyStrings = true), MaxLength(20)] public string Suffix { get; set; } = "";
	[EnumDataType(typeof(AdmissionNumberYearFormat))] public AdmissionNumberYearFormat YearFormat { get; set; } = AdmissionNumberYearFormat.CalendarYear;
	[EnumDataType(typeof(AdmissionNumberResetPolicy))] public AdmissionNumberResetPolicy ResetPolicy { get; set; } = AdmissionNumberResetPolicy.CalendarYear;
	[Range(1, 12)] public int Digits { get; set; } = 6;
	[Range(1, 999999999)] public long StartingNumber { get; set; } = 1;
	public bool BeginNewSeries { get; set; }
}

public class AdmissionApplicationNumberingDto : SaveAdmissionApplicationNumberingDto
{
	public bool IsConfigured { get; set; }
	public bool SequenceLocked { get; set; }
}

public class PreviewAdmissionApplicationNumberingDto : SaveAdmissionApplicationNumberingDto
{
	public Guid? AcademicYearId { get; set; }
}

public class AdmissionApplicationNumberPreviewDto
{
	public string NextNumber { get; set; } = "";
	public long NextSequence { get; set; }
	public bool StartingNumberUnavailable { get; set; }
}

public class AdmissionApplicationNumberingHistoryDto
{
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public string ChangedByName { get; set; } = "";
	public string Prefix { get; set; } = "";
	public string Suffix { get; set; } = "";
	public string Separator { get; set; } = "";
	public AdmissionNumberYearFormat YearFormat { get; set; }
	public AdmissionNumberResetPolicy ResetPolicy { get; set; }
	public int Digits { get; set; }
	public long StartingNumber { get; set; }
}

public interface IAdmissionApplicationNumberingAppService : IApplicationService
{
	Task<AdmissionApplicationNumberingDto> GetAsync();
	Task<List<AdmissionApplicationNumberingHistoryDto>> GetHistoryAsync();
	Task<AdmissionApplicationNumberingDto> SaveAsync(SaveAdmissionApplicationNumberingDto input);
	Task<AdmissionApplicationNumberPreviewDto> PreviewAsync(PreviewAdmissionApplicationNumberingDto input);
}
