using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public decimal MaximumSizeMb { get; private set; }
	public string AcceptedExtensions { get; private set; } = string.Empty;
	public bool RequiresOriginal { get; private set; }
	public bool ValidityDateRequired { get; private set; }
	public int DisplayOrder { get; private set; }
	public bool IsActive { get; private set; }

	protected AdmissionCrmDocumentType()
	{
	}

	public AdmissionCrmDocumentType(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string code,
		string name,
		decimal maximumSizeMb,
		IReadOnlyCollection<string> acceptedExtensions,
		bool requiresOriginal,
		bool validityDateRequired,
		int displayOrder)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Update(code, name, maximumSizeMb, acceptedExtensions, requiresOriginal, validityDateRequired, displayOrder);
		IsActive = true;
	}

	public void Update(
		string code,
		string name,
		decimal maximumSizeMb,
		IReadOnlyCollection<string> acceptedExtensions,
		bool requiresOriginal,
		bool validityDateRequired,
		int displayOrder)
	{
		Code = NormalizeCode(code);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		if (maximumSizeMb is < 0.1m or > 25m) throw new BusinessException("AdmissionCrm:InvalidDocumentTypeSize");
		var extensions = acceptedExtensions.Select(x => x.Trim().TrimStart('.').ToLowerInvariant()).Where(x => x.Length > 0).Distinct().Order().ToList();
		if (extensions.Count == 0 || extensions.Any(x => x.Any(c => !char.IsLetterOrDigit(c))))
			throw new BusinessException("AdmissionCrm:InvalidDocumentExtensions");
		if (displayOrder < 0) throw new BusinessException("AdmissionCrm:InvalidDocumentTypeDisplayOrder");
		MaximumSizeMb = maximumSizeMb;
		AcceptedExtensions = string.Join(',', extensions);
		RequiresOriginal = requiresOriginal;
		ValidityDateRequired = validityDateRequired;
		DisplayOrder = displayOrder;
	}

	public IReadOnlyList<string> GetAcceptedExtensions() => AcceptedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries);
	public void SetActive(bool active) => IsActive = active;

	private static string NormalizeCode(string value)
	{
		var code = Check.NotNullOrWhiteSpace(value, nameof(value), AdmissionCrmConsts.MaxPlaybookCodeLength).Trim().ToUpperInvariant();
		if (code.Any(x => !char.IsLetterOrDigit(x) && x is not '-' and not '_'))
			throw new BusinessException("AdmissionCrm:InvalidDocumentTypeCode");
		return code;
	}
}
