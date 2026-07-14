using System;
using System.Globalization;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFormNumberSeries : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Prefix { get; private set; } = string.Empty;
	public string Suffix { get; private set; } = string.Empty;
	public int Padding { get; private set; }
	public long NextNumber { get; private set; }
	public string ApplicabilityJson { get; private set; } = "{}";
	public bool IsActive { get; private set; }

	protected AdmissionCrmFormNumberSeries()
	{
	}

	public AdmissionCrmFormNumberSeries(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		string prefix,
		string suffix,
		int padding,
		long nextNumber,
		string applicabilityJson,
		bool isActive)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Update(name, prefix, suffix, padding, nextNumber, applicabilityJson, isActive);
	}

	public void Update(
		string name,
		string prefix,
		string suffix,
		int padding,
		long nextNumber,
		string applicabilityJson,
		bool isActive)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxTemplateNameLength);
		Prefix = NormalizePart(prefix, nameof(prefix));
		Suffix = NormalizePart(suffix, nameof(suffix));
		if (padding is < 1 or > 12 || nextNumber < 1)
		{
			throw new BusinessException("AdmissionCrm:InvalidFormNumberSeries");
		}
		Padding = padding;
		NextNumber = nextNumber;
		ApplicabilityJson = Check.Length(applicabilityJson, nameof(applicabilityJson), AdmissionCrmConsts.MaxRegistrationTemplateJsonLength);
		IsActive = isActive;
	}

	public string TakeNextNumber()
	{
		if (!IsActive)
		{
			throw new BusinessException("AdmissionCrm:FormNumberSeriesInactive");
		}
		var number = Prefix + NextNumber.ToString($"D{Padding}", CultureInfo.InvariantCulture) + Suffix;
		NextNumber++;
		return number;
	}

	private static string NormalizePart(string value, string name)
	{
		var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
		if (normalized.Length > AdmissionCrmConsts.MaxFormNumberPatternLength ||
		    normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
		{
			throw new BusinessException("AdmissionCrm:InvalidFormNumberSeriesPart").WithData("Part", name);
		}
		return normalized;
	}
}
