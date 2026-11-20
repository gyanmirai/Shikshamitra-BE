using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<FineCollectionExportEvidence> CaptureFineCollectionExportEvidenceAsync(Guid jobId) => WithUnitOfWorkAsync(async () =>
	{
		var job = await GetRequiredService<IHrReportExportJobRepository>().GetAsync(jobId);
		var properties = JsonSerializer.Serialize(job.ExtraProperties.OrderBy(property => property.Key, StringComparer.Ordinal)
			.Select(property => new { property.Key, Value = property.Value?.ToString() }));
		var bytes = string.IsNullOrWhiteSpace(job.BlobName) ? null :
			await GetRequiredService<IBlobContainer>().GetAllBytesOrNullAsync(job.BlobName);
		return new FineCollectionExportEvidence(job.RequestJson, job.FilterJson, properties, job.Status, job.BlobName, job.RowCount, bytes);
	});

	private async Task AssertFineCollectionExportRetainedAsync(Guid jobId, FineCollectionExportEvidence saved)
	{
		var actual = await CaptureFineCollectionExportEvidenceAsync(jobId);
		actual.RequestJson.ShouldBe(saved.RequestJson); actual.FilterJson.ShouldBe(saved.FilterJson);
		actual.PropertiesJson.ShouldBe(saved.PropertiesJson); actual.Status.ShouldBe(HrReportExportStatus.Completed);
		actual.BlobName.ShouldBe(saved.BlobName); actual.RowCount.ShouldBe(saved.RowCount);
		actual.Bytes.ShouldBe(saved.Bytes);
	}

	private sealed record FineCollectionExportEvidence(string RequestJson, string FilterJson, string PropertiesJson,
		string Status, string? BlobName, int? RowCount, byte[]? Bytes);
}

