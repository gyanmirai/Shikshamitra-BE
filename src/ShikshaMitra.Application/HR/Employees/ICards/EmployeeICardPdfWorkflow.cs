using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.Data;
using ShikshaMitra.TenantSettings.AcademicYears;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common.IdentityCards;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.HR.Employees.ICards;

[RemoteService(false)]
public class EmployeeICardPdfWorkflow(
	IRepository<EmployeeICardTemplate, Guid> templateRepository,
	IEmployeeRepository employeeRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver,
	EmployeeICardAssetService assetService,
	IAsyncQueryableExecuter asyncExecuter,
	SchoolIdentityDocumentService schoolIdentity,
	IRepository<AcademicYear, Guid> academicYears,
	IEmployeeAuthorizationService employeeAuthorization) : ApplicationService, ITransientDependency
{
	public async Task<IdentityCardPhotoDto> GetPhotoAsync(Guid employeeId)
	{
		await AuthorizeEmployeeAsync(employeeId);
		var employee = await employeeRepository.GetAsync(employeeId, includeDetails: false);
		if (employee.TenantId != CurrentTenant.Id) throw IdentityCardGeometry.Invalid("This employee is unavailable in the current school.");
		return IdentityCardPhotos.Read(employeeId, await assetService.TryGetBytesAsync(employee.ProfilePictureBlobName));
	}
	private async Task AuthorizeEmployeeAsync(Guid employeeId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.View);
		if (!CurrentUser.Id.HasValue) throw IdentityCardGeometry.Invalid("Sign in before accessing card photos.");
		await employeeAuthorization.CheckEmployeeScopedPermissionAsync(employeeId, ShikshaMitraSubmodulePermissions.HR.Employee_ICard.View);
	}
	public async Task<byte[]> GenerateEmployeeICardsPdfAsync(Guid templateId, EmployeeICardPdfRequest input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.View);
		if (input.EmployeeIds == null || input.EmployeeIds.Count is < 1 or > 100 || input.EmployeeIds.Any(id => id == Guid.Empty))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		var template = await templateRepository.GetAsync(templateId);
		if (template.TenantId != CurrentTenant.Id) throw IdentityCardGeometry.Invalid("This template is unavailable in the current school.");
		var employeeIds = input.EmployeeIds.Distinct().ToList();
		foreach (var employeeId in employeeIds) await AuthorizeEmployeeAsync(employeeId);
		var fields = EmployeeICardTemplateRules.ParseFieldsJson(template.FieldsJson);
		var layout = EmployeeICardLayoutSchema.ResolveLayout(template);
		var printSettings = EmployeeICardLayoutSchema.ResolvePrintSettings(template.PrintSettingsJson, input.CardsPerPage, template.SchemaVersion);
		EmployeeICardTemplateRules.ValidateTemplateAssetReferences(
			CurrentTenant.Id,
			template.LogoBlobName,
			template.BackgroundImageBlobName);

		var employees = await GetEmployeesAsync(employeeIds);
		if (employees.Any(e => e.TenantId != CurrentTenant.Id)) throw IdentityCardGeometry.Invalid("The selected employees are unavailable in the current school.");
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId),
			template.AcademicYearId);
		EnsureEmployeesMatchTemplateScope(template, employees, organizationsByUserId);
		var employeeById = employees.ToDictionary(e => e.Id);
		var photoEdits = await IdentityCardPhotos.ValidateAsync(input.PhotoEdits, employeeIds, async id =>
		{
			var original = await assetService.TryGetBytesAsync(employeeById[id].ProfilePictureBlobName);
			return original;
		});

		if (employees.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		var logoBytes = await assetService.TryGetBytesAsync(template.LogoBlobName);
		var logoIsRaster = IsRasterImage(template.LogoBlobName);
		var logoMode = (SchoolIdentityAssetMode?)template.GetProperty<int?>("SchoolIdentityLogoMode");
		if (logoMode == SchoolIdentityAssetMode.Hidden) logoBytes = null;
		else if (logoMode == SchoolIdentityAssetMode.Inherit || (logoMode == null && string.IsNullOrWhiteSpace(template.LogoBlobName)))
		{
			var identitySnapshot = await schoolIdentity.CaptureForStageAsync(template.MediumStageId);
			logoBytes = (await schoolIdentity.LoadAsync(identitySnapshot)).Logo;
			logoIsRaster = true;
		}
		employees = FilterIncompleteEmployees(input, template, employees, logoMode == SchoolIdentityAssetMode.Hidden || logoBytes != null);
		if (employees.Count == 0) throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		var photoBytesByEmployeeId = await GetPhotoBytesByEmployeeIdAsync(employees);
		foreach (var (id, edited) in photoEdits) if (photoBytesByEmployeeId.ContainsKey(id)) photoBytesByEmployeeId[id] = edited;
		string? validityYear = null;
		if (template.SchemaVersion >= 2)
		{
			var year = await academicYears.FindAsync(x => x.Id == template.AcademicYearId && x.TenantId == CurrentTenant.Id);
			validityYear = year?.Name ?? throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson).WithData("detail", "The template academic year is unavailable in this school.");
		}
		var cardsPerPage = input.CardsPerPage ?? printSettings.CardsPerPage;
		return EmployeeICardPdfComposer.Compose(
			template,
			employees,
			organizationsByUserId,
			fields,
			layout,
			printSettings,
			cardsPerPage,
			logoIsRaster ? logoBytes : null,
			photoBytesByEmployeeId,
			validityYear);
	}

	private async Task<List<Employee>> GetEmployeesAsync(List<Guid> employeeIds)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(
			employeeQuery
				.Where(x => employeeIds.Contains(x.Id))
				.OrderBy(x => x.FirstName)
				.ThenBy(x => x.LastName));

		if (employees.Count != employeeIds.Count)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeMissing);
		}

		return employees;
	}

	private static void EnsureEmployeesMatchTemplateScope(
		EmployeeICardTemplate template,
		List<Employee> employees,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		if (template.MediumStageId.HasValue &&
			employees.Any(employee => !employee.UserId.HasValue ||
				!organizationsByUserId.TryGetValue(employee.UserId.Value, out var organizations) ||
				organizations.All(x => x.Id != template.MediumStageId.Value)))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeMissing);
		}
	}

	private static List<Employee> FilterIncompleteEmployees(
		EmployeeICardPdfRequest input,
		EmployeeICardTemplate template,
		List<Employee> employees, bool hasLogo)
	{
		if (!input.ExcludeIncompleteEmployees)
		{
			return employees;
		}

		if (!hasLogo)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		return employees
			.Where(x => !string.IsNullOrWhiteSpace(x.ProfilePictureBlobName) &&
						!string.IsNullOrWhiteSpace(x.MobileNo) &&
						!string.IsNullOrWhiteSpace(x.EmployeeCode) &&
						!string.IsNullOrWhiteSpace(x.Designation))
			.ToList();
	}

	private async Task<Dictionary<Guid, byte[]?>> GetPhotoBytesByEmployeeIdAsync(List<Employee> employees)
	{
		var photoBytesByEmployeeId = new Dictionary<Guid, byte[]?>();
		foreach (var employee in employees)
		{
			photoBytesByEmployeeId[employee.Id] = IsRasterImage(employee.ProfilePictureBlobName)
				? await assetService.TryGetBytesAsync(employee.ProfilePictureBlobName)
				: null;
		}

		return photoBytesByEmployeeId;
	}

	private static bool IsRasterImage(string? blobName)
	{
		var extension = Path.GetExtension(blobName ?? string.Empty).ToLowerInvariant();
		return extension is ".png" or ".jpg" or ".jpeg";
	}
}
