using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class ConcessionTypeLifecycle_Tests
{
	[Fact]
	public void Constructor_Normalizes_Name_And_Starts_Active()
	{
		var concessionType = Create("  Merit   Scholarship  ");

		Assert.Equal("Merit Scholarship", concessionType.Name);
		Assert.Equal("MERIT SCHOLARSHIP", concessionType.NormalizedName);
		Assert.True(concessionType.IsActive);
	}

	[Fact]
	public void Deactivate_Requires_Reason_And_Activate_Clears_It()
	{
		var concessionType = Create("Merit Scholarship");

		var exception = Assert.Throws<BusinessException>(() => concessionType.Deactivate("  "));
		Assert.Equal(ConcessionTypeErrorCodes.DeactivationReasonRequired, exception.Code);

		concessionType.Deactivate("Policy retired");
		Assert.False(concessionType.IsActive);
		Assert.Equal("Policy retired", concessionType.DeactivationReason);

		concessionType.Activate();
		Assert.True(concessionType.IsActive);
		Assert.Null(concessionType.DeactivationReason);
	}

	[Fact]
	public void AddDocument_Preserves_Requirement_And_Formats()
	{
		var concessionType = Create("Sibling Concession");

		concessionType.AddDocument(
			"Sibling proof",
			5 * 1024 * 1024,
			new List<DocumentType> { DocumentType.Pdf, DocumentType.Jpg },
			isRequired: false);

		var document = Assert.Single(concessionType.ConcessionTypeDocuments);
		Assert.False(document.IsRequired);
		Assert.Equal(5 * 1024 * 1024, document.MaxFileSizeBytes);
		Assert.Equal(2, document.AcceptedFormats.Count);
	}

	private static ConcessionType Create(string name) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), name, "Description");
}
