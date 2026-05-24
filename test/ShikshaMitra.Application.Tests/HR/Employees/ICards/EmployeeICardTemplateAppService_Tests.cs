using System;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.ICards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Employees.ICards;

public class EmployeeICardTemplateAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEmployeeICardTemplateAppService _iCardAppService;
	private readonly IEmployeeAppService _employeeAppService;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public EmployeeICardTemplateAppService_Tests()
	{
		_iCardAppService = GetRequiredService<IEmployeeICardTemplateAppService>();
		_employeeAppService = GetRequiredService<IEmployeeAppService>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		QuestPDF.Settings.License = LicenseType.Community;
	}

	[Fact]
	public async Task CreateAsync_Should_Accept_Valid_Field_Schema()
	{
		var template = await _iCardAppService.CreateAsync(BuildTemplateRequest());

		template.Id.ShouldNotBe(Guid.Empty);
		template.FieldsJson.ShouldContain("employeeCode");
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Unknown_Field_Name()
	{
		var input = BuildTemplateRequest("""[{"name":"unknownField","enabled":true}]""");

		var exception = await Should.ThrowAsync<BusinessException>(() => _iCardAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeICardFieldSchemaUnsupported);
	}

	[Theory]
	[InlineData("{\"name\":\"employeeCode\"}")]
	[InlineData("[{\"enabled\":true}]")]
	[InlineData("not-json")]
	public async Task CreateAsync_Should_Reject_Malformed_Field_Schema(string fieldsJson)
	{
		var input = BuildTemplateRequest(fieldsJson);

		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.CreateAsync(input));
	}

	[Fact]
	public async Task UploadAssetAsync_Should_Reject_Invalid_Assets()
	{
		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.UploadAssetAsync("logo.png", "image/png", []));
		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.UploadAssetAsync("logo.bmp", "image/png", BuildMinimalPng()));
		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.UploadAssetAsync("logo.png", "text/plain", BuildMinimalPng()));
		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.UploadAssetAsync("logo.png", "image/png", [1, 2, 3]));
		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.UploadAssetAsync("logo.svg", "image/svg+xml", [60, 115, 118, 103, 62]));
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Invalid_Asset_References_And_Colors()
	{
		var invalidLogo = BuildTemplateRequest();
		invalidLogo.LogoBlobName = "employee-documents/logo.png";

		var logoException = await Should.ThrowAsync<BusinessException>(() => _iCardAppService.CreateAsync(invalidLogo));
		logoException.Code.ShouldBe(HRErrorCodes.EmployeeICardAssetBlobInvalid);

		var invalidColor = BuildTemplateRequest();
		invalidColor.ThemeColor = "not-a-color";

		await Should.ThrowAsync<BusinessException>(() => _iCardAppService.CreateAsync(invalidColor));
	}

	[Fact]
	public async Task DownloadAssetAsync_Should_Reject_Blob_Outside_Icard_Prefix()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() => _iCardAppService.DownloadAssetAsync("employee-documents/logo.png"));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeICardAssetBlobInvalid);
	}

	[Fact]
	public async Task Upload_Then_Download_Asset_Should_Return_Stored_Image()
	{
		var upload = await _iCardAppService.UploadAssetAsync("logo.png", "image/png", BuildMinimalPng());

		var download = await _iCardAppService.DownloadAssetAsync(upload.BlobName);

		download.Content.Length.ShouldBe(BuildMinimalPng().Length);
		download.ContentType.ShouldBe("image/png");
		download.FileName.ShouldContain("logo.png");
	}

	[Fact]
	public async Task GenerateEmployeeICardsPdfAsync_Should_Reject_Missing_Employee_Id()
	{
		var template = await _iCardAppService.CreateAsync(BuildTemplateRequest());

		var exception = await Should.ThrowAsync<BusinessException>(() => _iCardAppService.GenerateEmployeeICardsPdfAsync(
			template.Id,
			new EmployeeICardPdfRequest { EmployeeIds = [Guid.NewGuid()] }));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeICardPdfEmployeeMissing);
	}

	[Fact]
	public async Task GenerateEmployeeICardsPdfAsync_Should_Reject_Out_Of_Scope_Employee()
	{
		var input = BuildTemplateRequest();
		input.MediumStageId = Guid.NewGuid();
		var template = await _iCardAppService.CreateAsync(input);
		var employee = await CreateEmployeeAsync();

		var exception = await Should.ThrowAsync<BusinessException>(() => _iCardAppService.GenerateEmployeeICardsPdfAsync(
			template.Id,
			new EmployeeICardPdfRequest { EmployeeIds = [employee.Id] }));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeICardPdfEmployeeMissing);
	}

	[Fact]
	public async Task GenerateEmployeeICardsPdfAsync_Should_Error_When_Exclusion_Removes_All()
	{
		var template = await _iCardAppService.CreateAsync(BuildTemplateRequest());
		var employee = await CreateEmployeeAsync();

		var exception = await Should.ThrowAsync<BusinessException>(() => _iCardAppService.GenerateEmployeeICardsPdfAsync(
			template.Id,
			new EmployeeICardPdfRequest { EmployeeIds = [employee.Id], ExcludeIncompleteEmployees = true }));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
	}

	[Fact]
	public async Task GenerateEmployeeICardsPdfAsync_Should_Return_Pdf_For_Existing_Employee()
	{
		var template = await _iCardAppService.CreateAsync(BuildTemplateRequest(
			"""[{"name":"photo","enabled":true},{"name":"name","enabled":true},{"name":"employeeCode","enabled":true},{"name":"contactNo","enabled":false}]"""));
		var employee = await CreateEmployeeAsync();

		var pdf = await _iCardAppService.GenerateEmployeeICardsPdfAsync(
			template.Id,
			new EmployeeICardPdfRequest
			{
				EmployeeIds = [employee.Id],
				CardsPerPage = 8
			});

		pdf.Length.ShouldBeGreaterThan(100);
		pdf.Take(4).ToArray().ShouldBe([0x25, 0x50, 0x44, 0x46]);
	}

	private EmployeeICardTemplateRequest BuildTemplateRequest(string? fieldsJson = null)
	{
		return new EmployeeICardTemplateRequest
		{
			AcademicYearId = _guidGenerator.Create(),
			Name = $"I-Card {Guid.NewGuid():N}",
			Width = 360,
			Height = 230,
			SchoolName = "Test School",
			SchoolAddress = "Test Address",
			ThemeColor = "#1D4ED8",
			BackgroundColor = "#F8FAFC",
			Orientation = "Horizontal",
			PresetKey = "executive-clean",
			QrCodeEnabled = true,
			BarcodeEnabled = false,
			BackSideEnabled = false,
			FieldsJson = fieldsJson ?? """[{"name":"photo","enabled":true},{"name":"name","enabled":true},{"name":"employeeCode","enabled":true},{"name":"designation","enabled":true}]""",
			IsActive = true
		};
	}

	private async Task<EmployeeDto> CreateEmployeeAsync()
	{
		var roleId = await EnsureRoleExistsAsync();
		return await _employeeAppService.CreateBasicInfoAsync(new CreateEmployeeBasicInfoDto
		{
			FirstName = "Card",
			LastName = "Employee",
			MobileNo = "9876543210",
			Email = $"card.employee.{Guid.NewGuid():N}@test.com",
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId
		});
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var roles = await _roleRepository.GetListAsync();
		var existing = roles.FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var id = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id), autoSave: true);
		return id;
	}

	private static byte[] BuildMinimalPng()
	{
		return
		[
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
			0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
			0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
			0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
			0x54, 0x78, 0x9C, 0x62, 0x00, 0x00, 0x00, 0x02,
			0x00, 0x01, 0xE5, 0x27, 0xDE, 0xFC, 0x00, 0x00,
			0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42,
			0x60, 0x82
		];
	}
}
