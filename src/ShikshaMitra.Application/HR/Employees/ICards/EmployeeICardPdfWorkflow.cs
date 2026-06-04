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

namespace ShikshaMitra.HR.Employees.ICards;

[RemoteService(false)]
public class EmployeeICardPdfWorkflow(
	IRepository<EmployeeICardTemplate, Guid> templateRepository,
	IEmployeeRepository employeeRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver,
	EmployeeICardAssetService assetService,
	IAsyncQueryableExecuter asyncExecuter) : ApplicationService, ITransientDependency
{
	public async Task<byte[]> GenerateEmployeeICardsPdfAsync(Guid templateId, EmployeeICardPdfRequest input)
	{
		if (input.EmployeeIds.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		var template = await templateRepository.GetAsync(templateId);
		var employeeIds = input.EmployeeIds.Distinct().ToList();
		var fields = EmployeeICardTemplateRules.ParseFieldsJson(template.FieldsJson);
		var layout = EmployeeICardLayoutSchema.ResolveLayout(template);
		var printSettings = EmployeeICardLayoutSchema.ResolvePrintSettings(template.PrintSettingsJson, input.CardsPerPage);
		EmployeeICardTemplateRules.ValidateTemplateAssetReferences(
			CurrentTenant.Id,
			template.LogoBlobName,
			template.BackgroundImageBlobName);

		var employees = await GetEmployeesAsync(employeeIds);
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId),
			template.AcademicYearId);
		EnsureEmployeesMatchTemplateScope(template, employees, organizationsByUserId);
		employees = FilterIncompleteEmployees(input, template, employees);

		if (employees.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		var logoBytes = await assetService.TryGetBytesAsync(template.LogoBlobName);
		var logoIsRaster = IsRasterImage(template.LogoBlobName);
		var photoBytesByEmployeeId = await GetPhotoBytesByEmployeeIdAsync(employees);
		var cardsPerPage = Math.Clamp(input.CardsPerPage ?? printSettings.CardsPerPage, 1, 10);
		return EmployeeICardPdfComposer.Compose(
			template,
			employees,
			organizationsByUserId,
			fields,
			layout,
			printSettings,
			cardsPerPage,
			logoIsRaster ? logoBytes : null,
			photoBytesByEmployeeId);
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
		List<Employee> employees)
	{
		if (!input.ExcludeIncompleteEmployees)
		{
			return employees;
		}

		if (string.IsNullOrWhiteSpace(template.LogoBlobName))
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
