using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Setting.ChargeCategory;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Consumer_Source_Projection_Should_Retain_The_Bound_Version_After_Unrelated_Metadata_Save()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var currentStamp = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await repository.GetAsync(owner.AssessmentId);
			source.SetProperty("ProjectionTestMetadata", "Reviewed without changing accepted assessment facts.");
			await repository.UpdateAsync(source, true);
			return source.ConcurrencyStamp;
		});
		currentStamp.ShouldNotBe(context.Post.Source.SourceVersion);
		var projection = await ReadConsumerSourceProjectionAsync(owner.AssessmentId);
		projection.SourceVersion.ShouldBe(context.Post.Source.SourceVersion);
		projection.SourceVersion.ShouldNotBe(currentStamp);
		projection.ExpectedSourceLinkId.ShouldNotBeNull();
		projection.TenantId.ShouldBe(owner.TenantId);
	}

	[Theory]
	[InlineData("\u00a0\u2003")]
	[InlineData("\t  દંડ / José\u3000")]
	public async Task Consumer_Source_Projection_Should_Use_DotNet_Reason_Whitespace_Without_Changing_Raw_Remarks(string remarks)
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee, prepareSource: owner => WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await repository.GetAsync(owner.AssessmentId);
			source.Remarks = remarks;
			await repository.UpdateAsync(source, true);
		}));
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var expected = await WithUnitOfWorkAsync(async () =>
		{
			var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
			var category = await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().GetAsync(source.ChargeCategoryId);
			return (string.IsNullOrWhiteSpace(source.Remarks) ? category.ChargeType : source.Remarks).Trim();
		});
		var projection = await ReadConsumerSourceProjectionAsync(owner.AssessmentId);
		projection.RawSourceRemarks.ShouldBe(remarks);
		projection.CurrentLegacyResolvedReason.ShouldBe(expected);
		projection.AssessmentOriginal.ShouldBe(100.75m);
	}

	private Task<FinanceExternalPositionSource> ReadConsumerSourceProjectionAsync(Guid sourceId) => WithUnitOfWorkAsync(async () =>
	{
		var query = await GetRequiredService<ILibraryChargePositionSourceQuery>().GetQueryAsync();
		return await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query.Where(row => row.SourceId == sourceId));
	});
}
