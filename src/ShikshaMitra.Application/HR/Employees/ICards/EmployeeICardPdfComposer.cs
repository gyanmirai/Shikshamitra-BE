using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common.IdentityCards;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.Data;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardPdfComposer
{
	public static byte[] Compose(EmployeeICardTemplate template, IReadOnlyCollection<Employee> employees,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId, IReadOnlyDictionary<string, bool> fields,
		IdentityCardLayout layout, IdentityCardPrintSettings printSettings, int cardsPerPage, byte[]? logoBytes,
		IReadOnlyDictionary<Guid, byte[]?> photoBytesByEmployeeId, string? validityYear = null)
	{
		printSettings.CardsPerPage = cardsPerPage;
		var cards = employees.Select(employee =>
		{
			var organization = EmployeeICardPdfFieldResolver.ResolveOrganizationName(employee, organizationsByUserId);
			string Render(bool back) => IdentityCardSvgRenderer.Render(layout, back,
				element => EmployeeICardPdfFieldResolver.ResolveFieldValue(template, employee, organization, element.FieldKey, element.Value, validityYear),
				element => ShouldRenderElement(template, element, fields), logoBytes, photoBytesByEmployeeId.GetValueOrDefault(employee.Id),
				EmployeeICardPdfFieldResolver.BuildIdentityPayload(employee), employee.EmployeeCode ?? employee.Id.ToString("N"), template.SchemaVersion >= 2);
			return new IdentityCardArtwork(Render(false), template.BackSideEnabled ? Render(true) : null);
		}).ToList();
		return IdentityCardPdfComposer.Compose(layout, printSettings, cards, template.BackSideEnabled);
	}

	internal static bool ShouldRenderElement(EmployeeICardTemplate template, IdentityCardLayoutElement element, IReadOnlyDictionary<string, bool> fields)
	{
		if (element.Type == "logo") return template.GetProperty<int?>("SchoolIdentityLogoMode") != (int)SchoolIdentityAssetMode.Hidden;
		if (element.Type == "qr") return template.QrCodeEnabled;
		if (element.Type == "barcode") return template.BarcodeEnabled;
		if (element.Type == "photo") return EmployeeICardPdfFieldResolver.IsFieldEnabled(fields, "photo");
		return string.IsNullOrWhiteSpace(element.FieldKey) || element.FieldKey is "schoolName" or "schoolAddress" or "static" ||
			EmployeeICardPdfFieldResolver.IsFieldEnabled(fields, element.FieldKey);
	}
}
