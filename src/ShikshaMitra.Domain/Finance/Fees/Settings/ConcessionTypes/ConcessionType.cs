using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; }
	public string? Description { get; private set; }

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
		Name = name;
		Description = description;
		DiscountType = discountType;
		ConcessionTypeDocuments = new List<ConcessionTypeDocument>();
	}

	public void Update(
		string name,
		string? description,
		AmountType? discountType = null
	)
	{
		Name = name;
		Description = description;
		DiscountType = discountType;
	}

	public void AddDocument(string documentName, long documentSize, List<DocumentType> acceptedFormats)
	{
		var document = new ConcessionTypeDocument(Guid.NewGuid(), Id, documentName, documentSize);

		foreach (var format in acceptedFormats)
		{
			document.AddAcceptedFormat(format);
		}

		ConcessionTypeDocuments.Add(document);
	}

	public void ReplaceDocuments(List<(string DocumentName, long DocumentSize, List<DocumentType> AcceptedFormats)> documents)
	{
		ConcessionTypeDocuments.Clear();
		foreach (var d in documents)
		{
			var doc = new ConcessionTypeDocument(Guid.NewGuid(), Id, d.DocumentName, d.DocumentSize);
			foreach (var format in d.AcceptedFormats)
			{
				doc.AddAcceptedFormat(format);
			}
			ConcessionTypeDocuments.Add(doc);
		}
	}
}
