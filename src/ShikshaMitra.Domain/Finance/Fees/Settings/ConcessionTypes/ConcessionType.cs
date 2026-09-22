using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; }
	public string NormalizedName { get; private set; }
	public string? Description { get; private set; }
	public bool IsActive { get; private set; } = true;
	public string? DeactivationReason { get; private set; }

	/// <summary>
	/// Indicates whether this concession type applies as a percentage or a flat amount.
	/// Nullable to remain compatible with concession types created before this field was introduced.
	/// </summary>
	public AmountType? DiscountType { get; private set; }

	public virtual ICollection<ConcessionTypeDocument> ConcessionTypeDocuments { get; private set; } = new List<ConcessionTypeDocument>();

	private ConcessionType()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public ConcessionType(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		string? description = null,
		bool isActive = true,
		AmountType? discountType = null
	) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetDetails(name, description);
		IsActive = isActive;
		DiscountType = discountType;
		ConcessionTypeDocuments = new List<ConcessionTypeDocument>();
	}

	public void Update(
		string name,
		string? description,
		AmountType? discountType = null
	)
	{
		SetDetails(name, description);
		DiscountType = discountType;
	}

	public void Activate()
	{
		IsActive = true;
		DeactivationReason = null;
	}

	public void Deactivate(string reason)
	{
		var normalizedReason = reason?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedReason))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.DeactivationReasonRequired);
		}

		if (normalizedReason.Length > ConcessionTypeConsts.MaxDeactivationReasonLength)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.DeactivationReasonRequired);
		}

		IsActive = false;
		DeactivationReason = normalizedReason;
	}

	public void AddDocument(
		string documentName,
		long documentSize,
		List<DocumentType> acceptedFormats,
		bool isRequired = true)
	{
		EnsureDocumentNameIsUnique(documentName);
		if (acceptedFormats is null || acceptedFormats.Count == 0)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		var document = new ConcessionTypeDocument(
			Guid.NewGuid(), Id, documentName, documentSize, isRequired);

		foreach (var format in acceptedFormats)
		{
			document.AddAcceptedFormat(format);
		}

		ConcessionTypeDocuments.Add(document);
	}

	public void ReplaceDocuments(
		List<(string DocumentName, long DocumentSize, List<DocumentType> AcceptedFormats, bool IsRequired)> documents)
	{
		var normalizedNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (var d in documents)
		{
			if (d.AcceptedFormats is null || d.AcceptedFormats.Count == 0 ||
				!normalizedNames.Add(ConcessionTypeConsts.NormalizeNameKey(d.DocumentName)))
			{
				throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
			}
		}

		ConcessionTypeDocuments.Clear();
		foreach (var d in documents)
		{
			var doc = new ConcessionTypeDocument(
				Guid.NewGuid(), Id, d.DocumentName, d.DocumentSize, d.IsRequired);
			foreach (var format in d.AcceptedFormats)
			{
				doc.AddAcceptedFormat(format);
			}
			ConcessionTypeDocuments.Add(doc);
		}
	}

	private void EnsureDocumentNameIsUnique(string documentName)
	{
		var normalizedName = ConcessionTypeConsts.NormalizeNameKey(documentName);
		if (string.IsNullOrWhiteSpace(normalizedName) ||
			ConcessionTypeDocuments.Any(document =>
				ConcessionTypeConsts.NormalizeNameKey(document.DocumentName) == normalizedName))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}
	}

	private void SetDetails(string name, string? description)
	{
		var displayName = ConcessionTypeConsts.NormalizeDisplayName(name);
		if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > ConcessionTypeConsts.MaxNameLength)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.DuplicateName);
		}

		var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		if (normalizedDescription?.Length > ConcessionTypeConsts.MaxDescriptionLength)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		Name = displayName;
		NormalizedName = ConcessionTypeConsts.NormalizeNameKey(displayName);
		Description = normalizedDescription;
	}
}
