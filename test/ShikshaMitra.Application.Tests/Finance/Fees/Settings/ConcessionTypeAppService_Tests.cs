using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using ConcessionTypeEntity = ShikshaMitra.Finance.Fees.Settings.ConcessionTypes.ConcessionType;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class ConcessionTypeAppService_Tests :
	ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Update_Preserves_Legacy_Discount_Type_And_Unchanged_Document_Identity()
	{
		var repository = GetRequiredService<IRepository<ConcessionTypeEntity, Guid>>();
		var service = GetRequiredService<IConcessionTypeAppService>();
		var concessionType = new ConcessionTypeEntity(
			Guid.NewGuid(), null, Guid.NewGuid(), "Sibling Discount", "Before",
			discountType: AmountType.Flat);
		concessionType.AddDocument("Sibling proof", 5 * 1024 * 1024,
			[DocumentType.Pdf, DocumentType.Jpg]);
		await repository.InsertAsync(concessionType, autoSave: true);
		var documentId = concessionType.ConcessionTypeDocuments.Single().Id;

		await service.UpdateAsync(concessionType.Id, new ConcessionTypeRequest
		{
			Name = "Sibling Discount",
			Description = "After",
			Documents =
			[
				new ConcessionTypeDocumentDto
				{
					DocumentName = "Sibling proof",
					MaxFileSizeBytes = 5 * 1024 * 1024,
					AcceptedFormats = [DocumentType.Jpg, DocumentType.Pdf],
					IsRequired = true
				}
			]
		});

		var updated = await repository.GetAsync(concessionType.Id);
		Assert.Equal(AmountType.Flat, updated.DiscountType);
		Assert.Equal("After", updated.Description);
		Assert.Equal(documentId, updated.ConcessionTypeDocuments.Single().Id);
	}
}
