using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	public async Task<StudentCertificatePdfDto> GetPdfAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		var card = await LoadCardAsync(id);
		if (card.Status == "Voided") throw new BusinessException("Students:Records:DocumentVoided");
		await AddEventAsync(card, "Download", null);
		return await RenderPdfAsync(card);
	}
	[UnitOfWork(true)]
	public async Task<StudentCertificatePdfDto> ReprintAsync(Guid id, StudentDocumentActionDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add); ValidateReason(input);
		var card = await LoadCardAsync(id); card.MarkPrinted();
		var pdf = await RenderPdfAsync(card);
		await cards.UpdateAsync(card, autoSave: true); await AddEventAsync(card, "Reprint", input.Reason); return pdf;
	}
	[UnitOfWork(true)]
	public async Task<StudentIdCardDto> VoidAsync(Guid id, StudentDocumentActionDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete); ValidateReason(input);
		var card = await LoadCardAsync(id);
		if (card.Status != "Voided") { card.Void(); await cards.UpdateAsync(card, autoSave: true); await AddEventAsync(card, "Void", input.Reason); }
		return Map(card);
	}
	private static void ValidateReason(StudentDocumentActionDto input) { if (input == null || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Length > 2000) throw new BusinessException("Students:Records:DocumentReasonRequired"); }
	private async Task<StudentCertificatePdfDto> RenderPdfAsync(StudentIdCard card)
	{
		var dto = Map(card); var template = dto.TemplateSnapshot;
		byte[]? photo = null;
		var snapshot = JsonSerializer.Deserialize<Dictionary<string, string?>>(card.StudentSnapshotJson, JsonOptions)!;
		if (template.ShowPhoto && snapshot.TryGetValue("_PhotoBlobName", out var photoBlob) && !string.IsNullOrWhiteSpace(photoBlob))
		{
			photo = await blobs.GetAllBytesAsync(photoBlob);
			if (photo.Length > 5 * 1024 * 1024) throw new BusinessException("Students:Records:PhotoTooLarge");
		}
		QuestPDF.Settings.License = LicenseType.Community;
		var content = Document.Create(container => container.Page(page =>
		{
			page.Size(85.6f, 54, Unit.Millimetre); page.Margin(4, Unit.Millimetre); page.DefaultTextStyle(t => t.FontSize(7));
			page.Header().Column(col => { col.Item().Text(template.SchoolName).FontSize(11).Bold().FontColor(template.PrimaryColor).ClampLines(1); col.Item().Text(template.HeaderText).FontSize(8).ClampLines(1); });
			page.Content().PaddingVertical(3).Row(row =>
			{
				if (photo != null) row.ConstantItem(19, Unit.Millimetre).Height(25, Unit.Millimetre).PaddingRight(3).Image(photo).FitArea();
				row.RelativeItem().Column(col =>
				{
				foreach (var field in template.Fields) if (dto.StudentSnapshot.TryGetValue(field, out var value)) col.Item().Text($"{field}: {value ?? "-"}").ClampLines(1);
				col.Item().Text($"Card: {dto.CardNumber}").SemiBold().ClampLines(1);
				});
			});
			page.Footer().Text(template.FooterText).FontSize(6).ClampLines(2);
		})).GeneratePdf();
		return new() { FileName = $"{card.CardNumber}.pdf", Content = content };
	}
}
