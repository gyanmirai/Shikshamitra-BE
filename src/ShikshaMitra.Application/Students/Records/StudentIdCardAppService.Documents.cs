using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using QRCoder;
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
	public async Task<StudentCertificatePdfDto> ExportPdfAsync(StudentIdCardsPdfRequestDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		EnsureTenant(); ValidateStudents(input);
		if (input.CardsPerPage is < 1 or > 10) throw new BusinessException("Students:Records:InvalidIdTemplate");
		var template = await LoadTemplateAsync();
		var cards = new List<(StudentIdCardDto Card, byte[]? Photo, byte[]? Logo)>();
		foreach (var studentId in input.StudentIds)
		{
			var snapshot = await snapshots.CaptureAsync(studentId);
			var photo = await LoadPhotoAsync(template, snapshot.Fields);
			var frozen = await CaptureIdentityTemplateAsync(template, snapshot);
			var logo = (await schoolIdentity.LoadAsync(frozen.SchoolIdentity)).Logo;
			cards.Add((new StudentIdCardDto { StudentId = studentId, HasPhoto = photo != null,
				StudentSnapshot = PublicFields(snapshot.Fields), TemplateSnapshot = frozen }, photo, logo));
		}
		QuestPDF.Settings.License = LicenseType.Community;
		var vertical = template.Orientation == "Vertical";
		var perPage = Math.Min(input.CardsPerPage, vertical ? 9 : 10);
		var content = Document.Create(container =>
		{
			foreach (var pageCards in cards.Chunk(perPage))
			{
				container.Page(page =>
				{
					page.Size(PageSizes.A4); page.Margin(7, Unit.Millimetre);
					page.Content().Column(column =>
					{
						column.Spacing(2, Unit.Millimetre);
						foreach (var rowCards in pageCards.Chunk(vertical ? 3 : 2))
						{
							column.Item().Row(row =>
							{
								row.Spacing(2, Unit.Millimetre);
								foreach (var item in rowCards)
									row.ConstantItem(vertical ? 54 : 85.6f, Unit.Millimetre).Element(c => ComposeCard(c, item.Card, item.Photo, item.Logo));
							});
						}
					});
				});
			}
		}).GeneratePdf();
		return new() { FileName = $"Student-I-Cards-{Clock.Now:yyyy-MM-dd}.pdf", Content = content };
	}

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
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add); ValidateReason(input);
		var card = await LoadCardAsync(id);
		if (card.Status != "Voided") { card.Void(); await cards.UpdateAsync(card, autoSave: true); await AddEventAsync(card, "Void", input.Reason); }
		return Map(card);
	}
	private static void ValidateReason(StudentDocumentActionDto input) { if (input == null || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Length > 2000) throw new BusinessException("Students:Records:DocumentReasonRequired"); }
	private async Task<StudentCertificatePdfDto> RenderPdfAsync(StudentIdCard card)
	{
		var dto = Map(card); var template = dto.TemplateSnapshot;
		var snapshot = JsonSerializer.Deserialize<Dictionary<string, string?>>(card.StudentSnapshotJson, JsonOptions)!;
		var photo = await LoadPhotoAsync(template, snapshot);
		var logo = (await schoolIdentity.LoadAsync(template.SchoolIdentity)).Logo;
		QuestPDF.Settings.License = LicenseType.Community;
		var vertical = template.Orientation == "Vertical";
		var content = Document.Create(container => container.Page(page =>
		{
			page.Size(vertical ? 54 : 85.6f, vertical ? 85.6f : 54, Unit.Millimetre);
			page.Margin(0); page.Content().Element(c => ComposeCard(c, dto, photo, logo));
		})).GeneratePdf();
		return new() { FileName = $"{card.CardNumber}.pdf", Content = content };
	}

	private async Task<byte[]?> LoadPhotoAsync(StudentIdCardTemplateDto template, Dictionary<string, string?> snapshot)
	{
		if (!template.ShowPhoto || !snapshot.TryGetValue("_PhotoBlobName", out var blobName) || string.IsNullOrWhiteSpace(blobName)) return null;
		var photo = await blobs.GetAllBytesAsync(blobName);
		if (photo.Length > 5 * 1024 * 1024) throw new BusinessException("Students:Records:PhotoTooLarge");
		return photo;
	}

	internal static void ComposeCard(IContainer container, StudentIdCardDto card, byte[]? photo, byte[]? logo = null)
	{
		var template = card.TemplateSnapshot;
		var vertical = template.Orientation == "Vertical";
		var photoFirst = template.PresetKey == "photo-first";
		var minimal = template.PresetKey == "minimal";
		var background = template.BackgroundColor;
		container.Background(background).Border(0.5f).BorderColor(template.PrimaryColor).Padding(3, Unit.Millimetre).Column(column =>
		{
			column.Item().Row(header =>
			{
				if (logo != null) header.ConstantItem(8, Unit.Millimetre).Height(8, Unit.Millimetre).PaddingRight(1, Unit.Millimetre).Image(logo).FitArea();
				header.RelativeItem().AlignMiddle().Text(template.SchoolName).FontSize(vertical ? 9 : 11).Bold().FontColor(template.PrimaryColor).ClampLines(1);
			});
			if (!string.IsNullOrWhiteSpace(template.SchoolAddress)) column.Item().Text(template.SchoolAddress).FontSize(5).ClampLines(1);
			if (!minimal) column.Item().LineHorizontal(1).LineColor(template.PrimaryColor);
			column.Item().PaddingTop(2, Unit.Millimetre).Text(template.HeaderText).FontSize(7).SemiBold().ClampLines(1);
			column.Item().PaddingTop(2, Unit.Millimetre).Row(row =>
			{
				if (template.ShowPhoto) row.ConstantItem(photoFirst ? 25 : vertical ? 15 : 19, Unit.Millimetre).Height(photoFirst ? 29 : vertical ? 19 : 25, Unit.Millimetre).PaddingRight(2, Unit.Millimetre).Element(c =>
				{
					if (photo != null) c.Image(photo).FitArea(); else c.Border(0.5f).BorderColor(template.PrimaryColor).AlignCenter().AlignMiddle().Text("PHOTO").FontSize(5);
				});
				row.RelativeItem().Column(fields =>
				{
					foreach (var field in template.Fields)
						if (card.StudentSnapshot.TryGetValue(field, out var value)) fields.Item().Text($"{field}: {value ?? "-"}").FontSize(vertical ? 5.5f : 6.5f).ClampLines(1);
					if (card.CardNumber != "Preview") fields.Item().Text($"Card: {card.CardNumber}").FontSize(6).SemiBold().ClampLines(1);
				});
				if (template.ShowQrCode)
				{
					using var generator = new QRCodeGenerator();
					using var qrData = generator.CreateQrCode(card.StudentId.ToString("N"), QRCodeGenerator.ECCLevel.Q);
					row.ConstantItem(12, Unit.Millimetre).Height(12, Unit.Millimetre).Svg(new SvgQRCode(qrData).GetGraphic(4));
				}
			});
			column.Item().ExtendVertical().AlignBottom().Text(template.FooterText).FontSize(5).ClampLines(2);
		});
	}
}
