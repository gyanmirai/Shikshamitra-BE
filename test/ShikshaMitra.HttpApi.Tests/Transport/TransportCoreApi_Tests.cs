using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.Transport;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Transport;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Transport)]
public class TransportCoreApi_Tests : ShikshaMitraApiTestBase
{
	private static readonly byte[] MinimalPdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");
	private static readonly byte[] MinimalPngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lN8mxQAAAABJRU5ErkJggg==");

	public TransportCoreApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Stops_Should_Create_List_Update_Status_And_Filter_By_Academic_Medium()
	{
		var mediumId = Factory.SeededMediumId;
		var otherMediumId = await GetOtherMediumIdAsync(mediumId);
		var stopName = UniqueName("North Gate");

		var created = await CreateStopAsync(mediumId, stopName, "North");
		created.Data.ShouldNotBeNull();
		created.Data!.Status.ShouldBe(TransportRecordStatus.Active);

		var list = await GetAndReadAsync<Result<PagedResultDto<TransportStopDto>>>(
			$"/api/transport/stops?academicMediumId={mediumId}&pageNumber=1&pageSize=10&search={Uri.EscapeDataString(stopName)}");

		list.Data.ShouldNotBeNull();
		list.Data!.Items.Count(item => item.Id == created.Data.Id).ShouldBe(1);

		var isolatedList = await GetAndReadAsync<Result<PagedResultDto<TransportStopDto>>>(
			$"/api/transport/stops?academicMediumId={otherMediumId}&pageNumber=1&pageSize=10&search={Uri.EscapeDataString(stopName)}");

		isolatedList.Data.ShouldNotBeNull();
		isolatedList.Data!.Items.ShouldNotContain(item => item.Id == created.Data.Id);

		var statusResponse = await Client.PutAsJsonAsync(
			$"/api/transport/stops/{created.Data.Id}/status?academicMediumId={mediumId}",
			new ChangeTransportStatusDto { Status = TransportRecordStatus.Inactive, Remarks = "Closed for repair" },
			JsonOptions);

		statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var statusBody = await statusResponse.Content.ReadFromJsonAsync<Result<TransportStopDto>>(JsonOptions);
		statusBody.ShouldNotBeNull();
		statusBody!.Data.ShouldNotBeNull();
		statusBody.Data!.Status.ShouldBe(TransportRecordStatus.Inactive);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Routes_Should_Create_Assign_Stops_And_Update_Status()
	{
		var mediumId = Factory.SeededMediumId;
		var firstStop = (await CreateStopAsync(mediumId, UniqueName("Pickup One"), "East")).Data!;
		var secondStop = (await CreateStopAsync(mediumId, UniqueName("Pickup Two"), "East")).Data!;
		var routeName = UniqueName("Morning Route");
		var routeCode = $"TR{Guid.NewGuid():N}"[..10].ToUpperInvariant();

		var routeCreateResponse = await Client.PostAsJsonAsync(
			$"/api/transport/routes?academicMediumId={mediumId}",
			new CreateUpdateTransportRouteDto
			{
				RouteName = routeName,
				RouteCode = routeCode,
				RouteType = "pickup",
				Distance = 12.5m,
				StartPoint = "Campus",
				EndPoint = "North Gate",
				PickupSchedule = new TransportScheduleDto
				{
					StartTime = DateTimeOffset.UtcNow.Date.AddHours(7),
					EndTime = DateTimeOffset.UtcNow.Date.AddHours(8)
				}
			},
			JsonOptions);

		routeCreateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var routeBody = await routeCreateResponse.Content.ReadFromJsonAsync<Result<TransportRouteDto>>(JsonOptions);
		routeBody.ShouldNotBeNull();
		routeBody!.Data.ShouldNotBeNull();
		routeBody.Data!.RouteCode.ShouldBe(routeCode);

		var assignResponse = await Client.PostAsJsonAsync(
			$"/api/transport/routes/{routeBody.Data.Id}/stops?academicMediumId={mediumId}",
			new AssignTransportRouteStopsDto
			{
				Stops =
				{
					new TransportRouteStopInputDto
					{
						StopId = firstStop.Id,
						Order = 1,
						ExpectedArrivalTime = DateTimeOffset.UtcNow.Date.AddHours(7).AddMinutes(10),
						ExpectedDepartureTime = DateTimeOffset.UtcNow.Date.AddHours(7).AddMinutes(15)
					},
					new TransportRouteStopInputDto
					{
						StopId = secondStop.Id,
						Order = 2,
						ExpectedArrivalTime = DateTimeOffset.UtcNow.Date.AddHours(7).AddMinutes(25),
						ExpectedDepartureTime = DateTimeOffset.UtcNow.Date.AddHours(7).AddMinutes(30)
					}
				}
			},
			JsonOptions);

		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var assigned = await assignResponse.Content.ReadFromJsonAsync<Result<System.Collections.Generic.List<TransportRouteStopDto>>>(JsonOptions);
		assigned.ShouldNotBeNull();
		assigned!.Data.ShouldNotBeNull();
		assigned.Data!.Count.ShouldBe(2);
		assigned.Data.Select(stop => stop.Order).ShouldBe(new[] { 1, 2 });

		var detail = await GetAndReadAsync<Result<TransportRouteDto>>(
			$"/api/transport/routes/{routeBody.Data.Id}?academicMediumId={mediumId}");

		detail.Data.ShouldNotBeNull();
		detail.Data!.Stops.ShouldBe(2);
		detail.Data.StopData.Count.ShouldBe(2);

		var statusResponse = await Client.PutAsJsonAsync(
			$"/api/transport/routes/{routeBody.Data.Id}/status?academicMediumId={mediumId}",
			new ChangeTransportStatusDto { Status = TransportRecordStatus.Inactive, Remarks = "Season ended" },
			JsonOptions);

		statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var statusBody = await statusResponse.Content.ReadFromJsonAsync<Result<TransportRouteDto>>(JsonOptions);
		statusBody.ShouldNotBeNull();
		statusBody!.Data.ShouldNotBeNull();
		statusBody.Data!.Status.ShouldBe(TransportRecordStatus.Inactive);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Vehicle_Settings_Should_Support_Crud_And_Duplicate_Validation()
	{
		var mediumId = Factory.SeededMediumId;
		var vehicleTypeName = UniqueName("Shuttle");
		var documentName = UniqueName("Permit");
		var serviceName = UniqueName("Alignment");
		var fuelName = UniqueName("Hybrid");

		var vehicleTypes = await GetAndReadAsync<Result<List<TransportVehicleTypeDto>>>(
			$"/api/transport/vehicle-types?academicMediumId={mediumId}");
		vehicleTypes.Data.ShouldNotBeNull();
		vehicleTypes.Data!.ShouldContain(row => row.Name == "Bus");

		var createdVehicleType = await PostAndReadAsync<Result<TransportVehicleTypeDto>>(
			$"/api/transport/vehicle-types?academicMediumId={mediumId}",
			new CreateUpdateTransportVehicleTypeDto
			{
				Name = vehicleTypeName,
				ApproxSittingRange = "10-20",
				Description = "Small campus vehicle"
			});
		createdVehicleType.Data.ShouldNotBeNull();

		var duplicateVehicleType = await Client.PostAsJsonAsync(
			$"/api/transport/vehicle-types?academicMediumId={mediumId}",
			new CreateUpdateTransportVehicleTypeDto { Name = vehicleTypeName.ToLowerInvariant() },
			JsonOptions);
		duplicateVehicleType.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var updateVehicleType = await Client.PutAsJsonAsync(
			$"/api/transport/vehicle-types/{createdVehicleType.Data!.Id}?academicMediumId={mediumId}",
			new CreateUpdateTransportVehicleTypeDto
			{
				Name = $"{vehicleTypeName} Updated",
				ApproxSittingRange = "12-24",
				IsActive = false
			},
			JsonOptions);
		updateVehicleType.StatusCode.ShouldBe(HttpStatusCode.OK);

		var deleteVehicleType = await Client.DeleteAsync(
			$"/api/transport/vehicle-types/{createdVehicleType.Data.Id}?academicMediumId={mediumId}");
		deleteVehicleType.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var createdDocument = await PostAndReadAsync<Result<TransportRequiredVehicleDocumentDto>>(
			$"/api/transport/required-vehicle-documents?academicMediumId={mediumId}",
			new CreateUpdateTransportRequiredVehicleDocumentDto
			{
				Name = documentName,
				MaxSizeMb = 5,
				AcceptedFormats = new List<int> { 0, 1 },
				IsRequired = true
			});
		createdDocument.Data.ShouldNotBeNull();
		var duplicateDocument = await Client.PostAsJsonAsync(
			$"/api/transport/required-vehicle-documents?academicMediumId={mediumId}",
			new CreateUpdateTransportRequiredVehicleDocumentDto { Name = documentName, AcceptedFormats = new List<int> { 0 } },
			JsonOptions);
		duplicateDocument.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var createdService = await PostAndReadAsync<Result<TransportServiceTypeDto>>(
			$"/api/transport/servicing-types?academicMediumId={mediumId}",
			new CreateUpdateTransportServiceTypeDto { Name = serviceName });
		createdService.Data.ShouldNotBeNull();
		var duplicateService = await Client.PostAsJsonAsync(
			$"/api/transport/servicing-types?academicMediumId={mediumId}",
			new CreateUpdateTransportServiceTypeDto { Name = serviceName },
			JsonOptions);
		duplicateService.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var createdFuel = await PostAndReadAsync<Result<TransportFuelTypeDto>>(
			$"/api/transport/fuel-types?academicMediumId={mediumId}",
			new CreateUpdateTransportFuelTypeDto { Name = fuelName });
		createdFuel.Data.ShouldNotBeNull();
		var duplicateFuel = await Client.PostAsJsonAsync(
			$"/api/transport/fuel-types?academicMediumId={mediumId}",
			new CreateUpdateTransportFuelTypeDto { Name = fuelName },
			JsonOptions);
		duplicateFuel.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Vehicles_Should_Create_List_Detail_Update_Status_And_Filter_By_Academic_Medium()
	{
		var mediumId = Factory.SeededMediumId;
		var otherMediumId = await GetOtherMediumIdAsync(mediumId);
		var setup = await GetVehicleSetupAsync(mediumId);
		var registrationNo = UniqueRegistrationNo();

		var created = await CreateVehicleAsync(mediumId, setup.VehicleTypeId, setup.FuelTypeId, registrationNo);
		created.Data.ShouldNotBeNull();
		created.Data!.Status.ShouldBe(TransportVehicleStatus.Active);
		created.Data.RegistrationNo.ShouldBe(registrationNo);

		var list = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleDto>>>(
			$"/api/transport/vehicles?academicMediumId={mediumId}&pageNumber=1&pageSize=10&search={registrationNo}");
		list.Data.ShouldNotBeNull();
		list.Data!.Items.Count(row => row.Id == created.Data.Id).ShouldBe(1);

		var isolated = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleDto>>>(
			$"/api/transport/vehicles?academicMediumId={otherMediumId}&pageNumber=1&pageSize=10&search={registrationNo}");
		isolated.Data.ShouldNotBeNull();
		isolated.Data!.Items.ShouldNotContain(row => row.Id == created.Data.Id);

		var detail = await GetAndReadAsync<Result<TransportVehicleDto>>(
			$"/api/transport/vehicles/{created.Data.Id}?academicMediumId={mediumId}");
		detail.Data.ShouldNotBeNull();
		detail.Data!.VehicleTypeId.ShouldBe(setup.VehicleTypeId);

		var updateInput = BuildVehicleInput(setup.VehicleTypeId, setup.FuelTypeId, registrationNo);
		updateInput.VehicleName = "Updated fleet vehicle";
		updateInput.SeatingCapacity = 42;
		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/transport/vehicles/{created.Data.Id}?academicMediumId={mediumId}",
			updateInput,
			JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await updateResponse.Content.ReadFromJsonAsync<Result<TransportVehicleDto>>(JsonOptions);
		updated!.Data!.VehicleName.ShouldBe("Updated fleet vehicle");
		updated.Data.SeatingCapacity.ShouldBe(42);

		var statusResponse = await Client.PutAsJsonAsync(
			$"/api/transport/vehicles/{created.Data.Id}/status?academicMediumId={mediumId}",
			new ChangeTransportVehicleStatusDto { Status = TransportVehicleStatus.Maintenance, Remarks = "Service due" },
			JsonOptions);
		statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var statusBody = await statusResponse.Content.ReadFromJsonAsync<Result<TransportVehicleDto>>(JsonOptions);
		statusBody!.Data!.Status.ShouldBe(TransportVehicleStatus.Maintenance);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Vehicle_Documents_Should_Upload_List_Download_Replace_Delete_And_Reject_Invalid_File()
	{
		var mediumId = Factory.SeededMediumId;
		var setup = await GetVehicleSetupAsync(mediumId);
		var vehicle = (await CreateVehicleAsync(mediumId, setup.VehicleTypeId, setup.FuelTypeId, UniqueRegistrationNo())).Data!;

		var requiredDocuments = await GetAndReadAsync<Result<List<TransportRequiredVehicleDocumentDto>>>(
			$"/api/transport/required-vehicle-documents?academicMediumId={mediumId}");
		var requiredDocument = requiredDocuments.Data!.First();

		var pending = await GetAndReadAsync<Result<List<TransportVehicleDocumentDto>>>(
			$"/api/transport/vehicles/{vehicle.Id}/documents?academicMediumId={mediumId}");
		pending.Data.ShouldNotBeNull();
		pending.Data!.ShouldContain(row => row.RequiredDocumentId == requiredDocument.Id && row.Status == "Pending");

		using var upload = BuildDocumentUpload(requiredDocument.Id, "vehicle-permit.pdf", MinimalPdfBytes, "application/pdf");
		var uploadResponse = await Client.PostAsync(
			$"/api/transport/vehicles/{vehicle.Id}/documents?academicMediumId={mediumId}",
			upload);
		uploadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var uploaded = await uploadResponse.Content.ReadFromJsonAsync<Result<TransportVehicleDocumentDto>>(JsonOptions);
		uploaded!.Data.ShouldNotBeNull();
		uploaded.Data!.Status.ShouldBe("Uploaded");

		var download = await Client.GetAsync(
			$"/api/transport/vehicles/{vehicle.Id}/documents/{uploaded.Data.Id}?academicMediumId={mediumId}");
		download.StatusCode.ShouldBe(HttpStatusCode.OK);
		var downloadedBytes = await download.Content.ReadAsByteArrayAsync();
		downloadedBytes.Take(4).ShouldBe(MinimalPdfBytes.Take(4));

		using var replace = BuildDocumentUpload(requiredDocument.Id, "vehicle-permit-updated.pdf", MinimalPdfBytes, "application/pdf");
		var replaceResponse = await Client.PutAsync(
			$"/api/transport/vehicles/{vehicle.Id}/documents/{uploaded.Data.Id}?academicMediumId={mediumId}",
			replace);
		replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var invalid = BuildDocumentUpload(requiredDocument.Id, "bad.pdf", Encoding.UTF8.GetBytes("not a pdf"), "application/pdf");
		var invalidResponse = await Client.PostAsync(
			$"/api/transport/vehicles/{vehicle.Id}/documents?academicMediumId={mediumId}",
			invalid);
		invalidResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var delete = await Client.DeleteAsync(
			$"/api/transport/vehicles/{vehicle.Id}/documents/{uploaded.Data.Id}?academicMediumId={mediumId}");
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Vehicle_Compliance_And_Maintenance_Should_Save_And_List()
	{
		var mediumId = Factory.SeededMediumId;
		var setup = await GetVehicleSetupAsync(mediumId);
		var vehicle = (await CreateVehicleAsync(mediumId, setup.VehicleTypeId, setup.FuelTypeId, UniqueRegistrationNo())).Data!;

		var complianceResponse = await Client.PutAsJsonAsync(
			$"/api/transport/vehicles/{vehicle.Id}/compliance?academicMediumId={mediumId}",
			new TransportVehicleComplianceDto
			{
				InsuranceCompany = "National Insurance",
				PolicyNo = UniqueName("POL"),
				InsuranceIssueDate = DateTime.UtcNow.Date,
				InsuranceExpiryDate = DateTime.UtcNow.Date.AddYears(1),
				CoverageAmount = 100000,
				PucCertificateNo = UniqueName("PUC"),
				PucIssueDate = DateTime.UtcNow.Date,
				PucExpiryDate = DateTime.UtcNow.Date.AddMonths(6)
			},
			JsonOptions);
		complianceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var compliance = await complianceResponse.Content.ReadFromJsonAsync<Result<TransportVehicleComplianceDto>>(JsonOptions);
		compliance!.Data!.PolicyNo.ShouldNotBeNullOrWhiteSpace();

		using var maintenance = BuildMaintenanceUpload("Regular", DateTime.UtcNow.Date, MinimalPngBytes);
		var maintenanceResponse = await Client.PostAsync(
			$"/api/transport/vehicles/{vehicle.Id}/maintenance-records?academicMediumId={mediumId}",
			maintenance);
		maintenanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var createdMaintenance = await maintenanceResponse.Content.ReadFromJsonAsync<Result<TransportVehicleMaintenanceRecordDto>>(JsonOptions);
		createdMaintenance!.Data!.ServiceType.ShouldContain("Regular");

		var list = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleMaintenanceRecordDto>>>(
			$"/api/transport/vehicles/{vehicle.Id}/maintenance-records?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		list.Data.ShouldNotBeNull();
		list.Data!.Items.ShouldContain(row => row.Id == createdMaintenance.Data.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Staff_Should_Create_List_Update_Status_And_Filter_By_Academic_Medium()
	{
		var mediumId = Factory.SeededMediumId;
		var otherMediumId = await GetOtherMediumIdAsync(mediumId);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TransportDriver");

		var created = await CreateTransportStaffAsync(
			mediumId,
			employee.Id,
			TransportStaffRole.Driver,
			"DL-REGRESSION-01");
		created.Data.ShouldNotBeNull();
		created.Data!.EmployeeId.ShouldBe(employee.Id);
		created.Data.RoleId.ShouldBe(TransportStaffRole.Driver);
		created.Data.Status.ShouldBe(TransportStaffStatus.Active);

		var duplicate = await Client.PostAsJsonAsync(
			$"/api/transport/staff?academicMediumId={mediumId}",
			new CreateUpdateTransportStaffDto
			{
				EmployeeId = employee.Id,
				Role = TransportStaffRole.Driver,
				LicenseNo = "DL-DUPLICATE",
				LicenseExpiryDate = DateTime.UtcNow.Date.AddYears(2),
				ExperienceYears = 3
			},
			JsonOptions);
		duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var list = await GetAndReadAsync<Result<PagedResultDto<TransportStaffDto>>>(
			$"/api/transport/staff?academicMediumId={mediumId}&pageNumber=1&pageSize=10&search={Uri.EscapeDataString(employee.FirstName)}&role={(int)TransportStaffRole.Driver}&status={(int)TransportStaffStatus.Active}");
		list.Data.ShouldNotBeNull();
		list.Data!.Items.ShouldContain(row => row.Id == created.Data.Id);

		var isolated = await GetAndReadAsync<Result<PagedResultDto<TransportStaffDto>>>(
			$"/api/transport/staff?academicMediumId={otherMediumId}&pageNumber=1&pageSize=10&search={Uri.EscapeDataString(employee.FirstName)}");
		isolated.Data.ShouldNotBeNull();
		isolated.Data!.Items.ShouldNotContain(row => row.Id == created.Data.Id);

		var detail = await GetAndReadAsync<Result<TransportStaffDto>>(
			$"/api/transport/staff/{created.Data.Id}?academicMediumId={mediumId}");
		detail.Data.ShouldNotBeNull();
		detail.Data!.EmployeeId.ShouldBe(employee.Id);

		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/transport/staff/{created.Data.Id}?academicMediumId={mediumId}",
			new CreateUpdateTransportStaffDto
			{
				EmployeeId = employee.Id,
				Role = TransportStaffRole.Driver,
				LicenseNo = "DL-UPDATED-01",
				LicenseExpiryDate = DateTime.UtcNow.Date.AddYears(4),
				ExperienceYears = 7,
				Remarks = "Updated by regression test"
			},
			JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await updateResponse.Content.ReadFromJsonAsync<Result<TransportStaffDto>>(JsonOptions);
		updated!.Data!.LicenseNo.ShouldBe("DL-UPDATED-01");
		updated.Data.ExperienceYears.ShouldBe(7);

		var statusResponse = await Client.PutAsJsonAsync(
			$"/api/transport/staff/{created.Data.Id}/status?academicMediumId={mediumId}",
			new ChangeTransportStaffStatusDto
			{
				Status = TransportStaffStatus.Inactive,
				Remarks = "No longer assigned to transport"
			},
			JsonOptions);
		statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var statusBody = await statusResponse.Content.ReadFromJsonAsync<Result<TransportStaffDto>>(JsonOptions);
		statusBody!.Data!.Status.ShouldBe(TransportStaffStatus.Inactive);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Route_Assignment_Should_Assign_List_History_Block_Conflicts_And_Unassign()
	{
		var mediumId = Factory.SeededMediumId;
		var setup = await GetVehicleSetupAsync(mediumId);
		var vehicle = (await CreateVehicleAsync(mediumId, setup.VehicleTypeId, setup.FuelTypeId, UniqueRegistrationNo())).Data!;
		var route = (await CreateRouteAsync(mediumId, UniqueName("Assigned Route"))).Data!;
		var otherRoute = (await CreateRouteAsync(mediumId, UniqueName("Conflict Route"))).Data!;
		var driverEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TransportDriverAssign");
		var conductorEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TransportConductorAssign");
		var driver = (await CreateTransportStaffAsync(mediumId, driverEmployee.Id, TransportStaffRole.Driver, "DL-ASSIGN-01")).Data!;
		var conductor = (await CreateTransportStaffAsync(mediumId, conductorEmployee.Id, TransportStaffRole.Conductor)).Data!;

		var vehicleOptionsBefore = await GetAndReadAsync<Result<List<TransportOptionDto>>>(
			$"/api/transport/vehicles/options?academicMediumId={mediumId}&onlyAssignable=true");
		vehicleOptionsBefore.Data.ShouldNotBeNull();
		vehicleOptionsBefore.Data!.ShouldContain(row => row.Id == vehicle.Id);

		var staffOptionsBefore = await GetAndReadAsync<Result<List<TransportOptionDto>>>(
			$"/api/transport/staff/options?academicMediumId={mediumId}&role={(int)TransportStaffRole.Driver}&onlyAssignable=true");
		staffOptionsBefore.Data.ShouldNotBeNull();
		staffOptionsBefore.Data!.ShouldContain(row => row.Id == driver.Id);

		var assignmentResponse = await Client.PostAsJsonAsync(
			$"/api/transport/routes/{route.Id}/vehicle-assignment?academicMediumId={mediumId}",
			new AssignTransportRouteVehicleDto
			{
				VehicleId = vehicle.Id,
				DriverStaffId = driver.Id,
				ConductorStaffId = conductor.Id,
				EffectiveFrom = DateTime.UtcNow.Date,
				Remarks = "Initial regression assignment"
			},
			JsonOptions);
		assignmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var assignment = await assignmentResponse.Content.ReadFromJsonAsync<Result<TransportRouteAssignmentDto>>(JsonOptions);
		assignment!.Data.ShouldNotBeNull();
		assignment.Data!.RouteId.ShouldBe(route.Id);
		assignment.Data.VehicleId.ShouldBe(vehicle.Id);
		assignment.Data.DriverStaffId.ShouldBe(driver.Id);
		assignment.Data.ConductorStaffId.ShouldBe(conductor.Id);
		assignment.Data.IsCurrent.ShouldBeTrue();

		var currentAssignment = await GetAndReadAsync<Result<TransportRouteAssignmentDto?>>(
			$"/api/transport/routes/{route.Id}/vehicle-assignment?academicMediumId={mediumId}");
		currentAssignment.Data.ShouldNotBeNull();
		currentAssignment.Data!.Id.ShouldBe(assignment.Data.Id);

		var routeDetail = await GetAndReadAsync<Result<TransportRouteDto>>(
			$"/api/transport/routes/{route.Id}?academicMediumId={mediumId}");
		routeDetail.Data.ShouldNotBeNull();
		routeDetail.Data!.VehicleAssignment.ShouldNotBeNull();
		routeDetail.Data.VehicleAssignment!.DriverStaffId.ShouldBe(driver.Id);

		var vehicleOptionsAfter = await GetAndReadAsync<Result<List<TransportOptionDto>>>(
			$"/api/transport/vehicles/options?academicMediumId={mediumId}&onlyAssignable=true");
		vehicleOptionsAfter.Data.ShouldNotBeNull();
		vehicleOptionsAfter.Data!.ShouldNotContain(row => row.Id == vehicle.Id);

		var staffOptionsAfter = await GetAndReadAsync<Result<List<TransportOptionDto>>>(
			$"/api/transport/staff/options?academicMediumId={mediumId}&role={(int)TransportStaffRole.Driver}&onlyAssignable=true");
		staffOptionsAfter.Data.ShouldNotBeNull();
		staffOptionsAfter.Data!.ShouldNotContain(row => row.Id == driver.Id);

		var routeVehicleHistory = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleHistoryRowDto>>>(
			$"/api/transport/routes/{route.Id}/vehicle-history?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		routeVehicleHistory.Data.ShouldNotBeNull();
		routeVehicleHistory.Data!.Items.ShouldContain(row => row.Id == assignment.Data.Id && row.IsCurrent);

		var routeDriverHistory = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleHistoryRowDto>>>(
			$"/api/transport/routes/{route.Id}/driver-conductor-history?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		routeDriverHistory.Data.ShouldNotBeNull();
		routeDriverHistory.Data!.Items.ShouldContain(row => row.DriverStaffId == driver.Id && row.ConductorStaffId == conductor.Id);

		var vehicleRouteHistory = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleHistoryRowDto>>>(
			$"/api/transport/vehicles/{vehicle.Id}/route-history?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		vehicleRouteHistory.Data.ShouldNotBeNull();
		vehicleRouteHistory.Data!.Items.ShouldContain(row => row.RouteId == route.Id && row.VehicleId == vehicle.Id);

		var vehicleDriverHistory = await GetAndReadAsync<Result<PagedResultDto<TransportVehicleHistoryRowDto>>>(
			$"/api/transport/vehicles/{vehicle.Id}/driver-history?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		vehicleDriverHistory.Data.ShouldNotBeNull();
		vehicleDriverHistory.Data!.Items.ShouldContain(row => row.DriverStaffId == driver.Id && row.ConductorStaffId == conductor.Id);

		var conflict = await Client.PostAsJsonAsync(
			$"/api/transport/routes/{otherRoute.Id}/vehicle-assignment?academicMediumId={mediumId}",
			new AssignTransportRouteVehicleDto
			{
				VehicleId = vehicle.Id,
				DriverStaffId = driver.Id,
				ConductorStaffId = conductor.Id,
				EffectiveFrom = DateTime.UtcNow.Date
			},
			JsonOptions);
		conflict.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var deleteResponse = await Client.DeleteAsync(
			$"/api/transport/routes/{route.Id}/vehicle-assignment?academicMediumId={mediumId}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var assignmentAfterDelete = await GetAndReadAsync<Result<TransportRouteAssignmentDto?>>(
			$"/api/transport/routes/{route.Id}/vehicle-assignment?academicMediumId={mediumId}");
		assignmentAfterDelete.Data.ShouldBeNull();

		var reassignmentResponse = await Client.PostAsJsonAsync(
			$"/api/transport/routes/{otherRoute.Id}/vehicle-assignment?academicMediumId={mediumId}",
			new AssignTransportRouteVehicleDto
			{
				VehicleId = vehicle.Id,
				DriverStaffId = driver.Id,
				EffectiveFrom = DateTime.UtcNow.Date.AddDays(1),
				Remarks = "Reassigned after unassign"
			},
			JsonOptions);
		reassignmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var reassignment = await reassignmentResponse.Content.ReadFromJsonAsync<Result<TransportRouteAssignmentDto>>(JsonOptions);
		reassignment!.Data!.RouteId.ShouldBe(otherRoute.Id);
		reassignment.Data.ConductorStaffId.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Transport_Applications_Should_Approve_Student_And_Create_Member_Assignment()
	{
		var mediumId = Factory.SeededMediumId;
		var student = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "TransportPassenger");
		var stop = (await CreateStopAsync(mediumId, UniqueName("Member Stop"), "Passenger")).Data!;
		var route = (await CreateRouteAsync(mediumId, UniqueName("Member Route"))).Data!;
		await AssignRouteStopsAsync(mediumId, route.Id, stop.Id);

		var candidates = await GetAndReadAsync<Result<PagedResultDto<TransportMemberCandidateDto>>>(
			$"/api/transport/member-candidates?academicMediumId={mediumId}&memberType={(int)TransportMemberType.Student}&search={Uri.EscapeDataString(student.FirstName)}&pageNumber=1&pageSize=10");
		candidates.Data.ShouldNotBeNull();
		candidates.Data!.Items.ShouldContain(row => row.SourceId == student.StudentId && row.MemberType == TransportMemberType.Student);

		var application = await CreateTransportApplicationAsync(
			mediumId,
			new CreateUpdateTransportApplicationDto
			{
				ApplicationType = TransportApplicationType.NewEnrollment,
				MemberType = TransportMemberType.Student,
				StudentId = student.StudentId,
				RequestedStopId = stop.Id,
				PickupType = "Pickup",
				Landmark = "Near library",
				AreaLocality = "North block",
				Description = "Student transport enrollment"
			});

		var suggestions = await GetAndReadAsync<Result<List<TransportSuggestedRouteDto>>>(
			$"/api/transport/applications/{application.Id}/suggested-routes?academicMediumId={mediumId}");
		suggestions.Data.ShouldNotBeNull();
		suggestions.Data!.ShouldContain(row => row.RouteId == route.Id && row.Stops.Any(stopRow => stopRow.StopId == stop.Id));

		var assigned = await AssignApplicationRouteStopAsync(mediumId, application.Id, route.Id, stop.Id);
		assigned.AssignedRouteId.ShouldBe(route.Id);
		assigned.AssignedStopId.ShouldBe(stop.Id);
		assigned.EstimatedFeeAmount.ShouldBe(stop.Amount);

		var approved = await ChangeApplicationStatusAsync(
			mediumId,
			application.Id,
			TransportApplicationStatus.Approved,
			"Approved by transport desk");
		approved.Status.ShouldBe(TransportApplicationStatus.Approved);
		approved.MemberId.ShouldNotBeNull();

		var members = await GetAndReadAsync<Result<PagedResultDto<TransportMemberDto>>>(
			$"/api/transport/members?academicMediumId={mediumId}&memberType={(int)TransportMemberType.Student}&search={Uri.EscapeDataString(student.FirstName)}&pageNumber=1&pageSize=10");
		members.Data.ShouldNotBeNull();
		members.Data!.Items.ShouldContain(row =>
			row.SourceId == student.StudentId &&
			row.Status == TransportMemberStatus.Active &&
			row.CurrentRouteId == route.Id &&
			row.CurrentStopId == stop.Id);

		var member = members.Data.Items.Single(row => row.SourceId == student.StudentId);
		var assignments = await GetAndReadAsync<Result<PagedResultDto<TransportMemberRouteAssignmentDto>>>(
			$"/api/transport/members/{member.Id}/assignments?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		assignments.Data.ShouldNotBeNull();
		assignments.Data!.Items.ShouldContain(row => row.RouteId == route.Id && row.StopId == stop.Id && row.IsCurrent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Transport_Applications_Should_Block_Duplicate_Pending_Applications()
	{
		var mediumId = Factory.SeededMediumId;
		var student = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "TransportDuplicate");
		var stop = (await CreateStopAsync(mediumId, UniqueName("Duplicate Stop"), "Passenger")).Data!;

		await CreateTransportApplicationAsync(
			mediumId,
			new CreateUpdateTransportApplicationDto
			{
				ApplicationType = TransportApplicationType.NewEnrollment,
				MemberType = TransportMemberType.Student,
				StudentId = student.StudentId,
				RequestedStopId = stop.Id,
				PickupType = "Pickup"
			});

		var duplicate = await Client.PostAsJsonAsync(
			$"/api/transport/applications?academicMediumId={mediumId}",
			new CreateUpdateTransportApplicationDto
			{
				ApplicationType = TransportApplicationType.NewEnrollment,
				MemberType = TransportMemberType.Student,
				StudentId = student.StudentId,
				RequestedStopId = stop.Id,
				PickupType = "Pickup"
			},
			JsonOptions);

		duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Transport_Member_Candidates_Should_Return_Employees()
	{
		var mediumId = Factory.SeededMediumId;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TransportPassengerEmployee");

		var candidates = await GetAndReadAsync<Result<PagedResultDto<TransportMemberCandidateDto>>>(
			$"/api/transport/member-candidates?academicMediumId={mediumId}&memberType={(int)TransportMemberType.Employee}&search={Uri.EscapeDataString(employee.FullName)}&pageNumber=1&pageSize=10");

		candidates.Data.ShouldNotBeNull();
		candidates.Data!.Items.ShouldContain(row =>
			row.SourceId == employee.Id &&
			row.MemberType == TransportMemberType.Employee &&
			row.Name == employee.FullName);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Stop_Change_Approval_Should_Close_Old_Assignment_And_Create_Current_Assignment()
	{
		var mediumId = Factory.SeededMediumId;
		var student = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "TransportStopChange");
		var firstStop = (await CreateStopAsync(mediumId, UniqueName("Old Stop"), "Passenger")).Data!;
		var secondStop = (await CreateStopAsync(mediumId, UniqueName("New Stop"), "Passenger")).Data!;
		var firstRoute = (await CreateRouteAsync(mediumId, UniqueName("Old Route"))).Data!;
		var secondRoute = (await CreateRouteAsync(mediumId, UniqueName("New Route"))).Data!;
		await AssignRouteStopsAsync(mediumId, firstRoute.Id, firstStop.Id);
		await AssignRouteStopsAsync(mediumId, secondRoute.Id, secondStop.Id);

		var enrollment = await CreateTransportApplicationAsync(
			mediumId,
			new CreateUpdateTransportApplicationDto
			{
				ApplicationType = TransportApplicationType.NewEnrollment,
				MemberType = TransportMemberType.Student,
				StudentId = student.StudentId,
				RequestedStopId = firstStop.Id,
				PickupType = "Pickup"
			});
		await AssignApplicationRouteStopAsync(mediumId, enrollment.Id, firstRoute.Id, firstStop.Id);
		var approvedEnrollment = await ChangeApplicationStatusAsync(mediumId, enrollment.Id, TransportApplicationStatus.Approved, "Initial approval");
		approvedEnrollment.MemberId.ShouldNotBeNull();

		var stopChange = await CreateTransportApplicationAsync(
			mediumId,
			new CreateUpdateTransportApplicationDto
			{
				ApplicationType = TransportApplicationType.StopChange,
				MemberType = TransportMemberType.Student,
				StudentId = student.StudentId,
				MemberId = approvedEnrollment.MemberId,
				RequestedStopId = secondStop.Id,
				PickupType = "Pickup",
				Description = "Family moved"
			});
		await AssignApplicationRouteStopAsync(mediumId, stopChange.Id, secondRoute.Id, secondStop.Id);
		var approvedStopChange = await ChangeApplicationStatusAsync(mediumId, stopChange.Id, TransportApplicationStatus.Approved, "Stop changed");
		approvedStopChange.Status.ShouldBe(TransportApplicationStatus.Approved);

		var member = await GetAndReadAsync<Result<TransportMemberDto>>(
			$"/api/transport/members/{approvedEnrollment.MemberId}?academicMediumId={mediumId}");
		member.Data.ShouldNotBeNull();
		member.Data!.CurrentRouteId.ShouldBe(secondRoute.Id);
		member.Data.CurrentStopId.ShouldBe(secondStop.Id);

		var assignments = await GetAndReadAsync<Result<PagedResultDto<TransportMemberRouteAssignmentDto>>>(
			$"/api/transport/members/{approvedEnrollment.MemberId}/assignments?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		assignments.Data.ShouldNotBeNull();
		assignments.Data!.Items.Count.ShouldBe(2);
		assignments.Data.Items.Count(row => row.IsCurrent).ShouldBe(1);
		assignments.Data.Items.ShouldContain(row => row.RouteId == firstRoute.Id && row.EffectiveTo.HasValue);
		assignments.Data.Items.ShouldContain(row => row.RouteId == secondRoute.Id && row.IsCurrent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Transport_Billing_Settings_Should_Support_Zone_Area_And_Fee_Configuration()
	{
		var mediumId = Factory.SeededMediumId;
		var chargeCategoryId = await CreateChargeCategoryAsync();
		var zoneName = UniqueName("Transport Zone");

		var createdZoneResponse = await Client.PostAsJsonAsync(
			$"/api/transport/zones-areas?academicMediumId={mediumId}",
			new CreateUpdateTransportZoneAreaDto
			{
				Name = zoneName,
				Description = "Billing zone",
				IsActive = true
			},
			JsonOptions);

		createdZoneResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var createdZone = await createdZoneResponse.Content.ReadFromJsonAsync<Result<TransportZoneAreaDto>>(JsonOptions);
		createdZone.ShouldNotBeNull();
		createdZone!.Data.ShouldNotBeNull();
		createdZone.Data!.Name.ShouldBe(zoneName);

		var duplicateZone = await Client.PostAsJsonAsync(
			$"/api/transport/zones-areas?academicMediumId={mediumId}",
			new CreateUpdateTransportZoneAreaDto
			{
				Name = zoneName,
				IsActive = true
			},
			JsonOptions);
		duplicateZone.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var zoneList = await GetAndReadAsync<Result<List<TransportZoneAreaDto>>>(
			$"/api/transport/zones-areas?academicMediumId={mediumId}");
		zoneList.Data.ShouldNotBeNull();
		zoneList.Data!.ShouldContain(row => row.Id == createdZone.Data.Id);

		var updatedZoneResponse = await Client.PutAsJsonAsync(
			$"/api/transport/zones-areas/{createdZone.Data.Id}?academicMediumId={mediumId}",
			new CreateUpdateTransportZoneAreaDto
			{
				Name = $"{zoneName} Updated",
				Description = "Inactive billing zone",
				IsActive = false
			},
			JsonOptions);

		updatedZoneResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updatedZone = await updatedZoneResponse.Content.ReadFromJsonAsync<Result<TransportZoneAreaDto>>(JsonOptions);
		updatedZone.ShouldNotBeNull();
		updatedZone!.Data.ShouldNotBeNull();
		updatedZone.Data!.IsActive.ShouldBeFalse();

		var deleteZoneResponse = await Client.DeleteAsync(
			$"/api/transport/zones-areas/{createdZone.Data.Id}?academicMediumId={mediumId}");
		deleteZoneResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var frequencyOptions = await GetAndReadAsync<Result<List<TransportOptionDto>>>("/api/transport/fee-frequency-options");
		frequencyOptions.Data.ShouldNotBeNull();
		frequencyOptions.Data!.ShouldContain(option => option.Label == "Monthly");

		var createConfigResponse = await Client.PostAsJsonAsync(
			$"/api/transport/fee-configuration?academicMediumId={mediumId}",
			new CreateUpdateTransportFeeConfigurationDto
			{
				ModelType = TransportFeeModelType.StopBased,
				FeeFrequency = TransportFeeFrequency.Monthly,
				ChargeCategoryId = chargeCategoryId,
				EffectiveFrom = DateTime.UtcNow.Date,
				IsActive = false,
				Remarks = "Inactive test configuration"
			},
			JsonOptions);

		createConfigResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var createdConfig = await createConfigResponse.Content.ReadFromJsonAsync<Result<TransportFeeConfigurationDto>>(JsonOptions);
		createdConfig.ShouldNotBeNull();
		createdConfig!.Data.ShouldNotBeNull();
		createdConfig.Data!.ModelType.ShouldBe(TransportFeeModelType.StopBased);
		createdConfig.Data.IsActive.ShouldBeFalse();

		var updateConfigResponse = await Client.PutAsJsonAsync(
			$"/api/transport/fee-configuration/{createdConfig.Data.Id}?academicMediumId={mediumId}",
			new CreateUpdateTransportFeeConfigurationDto
			{
				ModelType = TransportFeeModelType.DistanceBased,
				FeeFrequency = TransportFeeFrequency.Quarterly,
				RatePerKm = 12.5m,
				ChargeCategoryId = chargeCategoryId,
				EffectiveFrom = DateTime.UtcNow.Date,
				IsActive = false,
				Remarks = "Updated inactive test configuration"
			},
			JsonOptions);

		updateConfigResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updatedConfig = await updateConfigResponse.Content.ReadFromJsonAsync<Result<TransportFeeConfigurationDto>>(JsonOptions);
		updatedConfig.ShouldNotBeNull();
		updatedConfig!.Data.ShouldNotBeNull();
		updatedConfig.Data!.ModelType.ShouldBe(TransportFeeModelType.DistanceBased);
		updatedConfig.Data.RatePerKm.ShouldBe(12.5m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Transport_Fee_Run_Should_Generate_Dues_And_Post_Student_Dues_To_Finance()
	{
		var mediumId = Factory.SeededMediumId;
		var academicYearId = Factory.SeededAcademicYearId;
		var chargeCategoryId = await CreateChargeCategoryAsync(isApprovalRequired: false);
		var stop = (await CreateStopAsync(mediumId, UniqueName("Billing Stop"), "Billing")).Data!;
		var route = (await CreateRouteAsync(mediumId, UniqueName("Billing Route"))).Data!;
		await AssignRouteStopsAsync(mediumId, route.Id, stop.Id);

		var periodYear = 1990 + (Guid.NewGuid().ToByteArray()[0] % 20);
		var periodFrom = new DateTime(periodYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var periodTo = new DateTime(periodYear, 1, 31, 0, 0, 0, DateTimeKind.Utc);
		var dueDate = DateTime.UtcNow.Date.AddDays(30);
		var effectiveDate = periodFrom.AddDays(4);

		var student = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "TransportBillingStudent");
		await MoveRegistrationToEnrolledAsync(student.StudentId);
		var studentMember = await CreateApprovedTransportMemberAsync(
			mediumId,
			TransportMemberType.Student,
			student.StudentId,
			route.Id,
			stop.Id,
			effectiveDate);

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "TransportBillingEmployee");
		var employeeMember = await CreateApprovedTransportMemberAsync(
			mediumId,
			TransportMemberType.Employee,
			employee.Id,
			route.Id,
			stop.Id,
			effectiveDate);

		var configuration = await EnsureActiveFeeConfigurationAsync(mediumId, chargeCategoryId);
		var runRequest = new TransportFeeRunRequestDto
		{
			AcademicYearId = academicYearId,
			FeeConfigurationId = configuration.Id,
			PeriodFrom = periodFrom,
			PeriodTo = periodTo,
			DueDate = dueDate,
			Remarks = "Transport billing regression"
		};

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/transport/fees/runs/preview?academicMediumId={mediumId}",
			runRequest,
			JsonOptions);

		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<Result<TransportFeeRunPreviewDto>>(JsonOptions);
		preview.ShouldNotBeNull();
		preview!.Data.ShouldNotBeNull();
		preview.Data!.GeneratedDueCount.ShouldBe(2);
		preview.Data.StudentDueCount.ShouldBe(1);
		preview.Data.EmployeeDueCount.ShouldBe(1);
		preview.Data.TotalAmount.ShouldBe(500m);

		var createRunResponse = await Client.PostAsJsonAsync(
			$"/api/transport/fees/runs?academicMediumId={mediumId}",
			runRequest,
			JsonOptions);

		createRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var createdRun = await createRunResponse.Content.ReadFromJsonAsync<Result<TransportFeeRunDto>>(JsonOptions);
		createdRun.ShouldNotBeNull();
		createdRun!.Data.ShouldNotBeNull();
		createdRun.Data!.GeneratedDueCount.ShouldBe(2);

		var duplicateRunResponse = await Client.PostAsJsonAsync(
			$"/api/transport/fees/runs?academicMediumId={mediumId}",
			runRequest,
			JsonOptions);
		duplicateRunResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var dues = await GetAndReadAsync<Result<PagedResultDto<TransportFeeDueDto>>>(
			$"/api/transport/fees/dues?academicMediumId={mediumId}&academicYearId={academicYearId}&runId={createdRun.Data.Id}&pageNumber=1&pageSize=10");
		dues.Data.ShouldNotBeNull();
		dues.Data!.Items.Count.ShouldBe(2);
		dues.Data.Items.ShouldContain(due => due.MemberId == studentMember.Id && due.Status == TransportFeeDueStatus.Pending);
		dues.Data.Items.ShouldContain(due => due.MemberId == employeeMember.Id && due.Status == TransportFeeDueStatus.Pending);

		var postFinanceResponse = await Client.PostAsync(
			$"/api/transport/fees/runs/{createdRun.Data.Id}/post-finance?academicMediumId={mediumId}",
			content: null);

		postFinanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var postFinance = await postFinanceResponse.Content.ReadFromJsonAsync<Result<TransportFeeFinancePostResultDto>>(JsonOptions);
		postFinance.ShouldNotBeNull();
		postFinance!.Data.ShouldNotBeNull();
		postFinance.Data!.PostedStudentDueCount.ShouldBe(1);
		postFinance.Data.SkippedEmployeeDueCount.ShouldBe(1);
		postFinance.Data.ChargeRequestCount.ShouldBe(1);
		postFinance.Data.Run.Status.ShouldBe(TransportFeeRunStatus.FinancePosted);

		var studentDues = await GetAndReadAsync<Result<PagedResultDto<TransportFeeDueDto>>>(
			$"/api/transport/members/{studentMember.Id}/dues?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		studentDues.Data.ShouldNotBeNull();
		var postedStudentDue = studentDues.Data!.Items.Single(due => due.FeeRunId == createdRun.Data.Id);
		postedStudentDue.Status.ShouldBe(TransportFeeDueStatus.PostedToFinance);
		postedStudentDue.FinanceChargeRequestId.ShouldNotBeNull();
		postedStudentDue.FinanceChargeLineId.ShouldNotBeNull();

		var employeeDues = await GetAndReadAsync<Result<PagedResultDto<TransportFeeDueDto>>>(
			$"/api/transport/members/{employeeMember.Id}/dues?academicMediumId={mediumId}&pageNumber=1&pageSize=10");
		employeeDues.Data.ShouldNotBeNull();
		var nativeEmployeeDue = employeeDues.Data!.Items.Single(due => due.FeeRunId == createdRun.Data.Id);
		nativeEmployeeDue.Status.ShouldBe(TransportFeeDueStatus.Pending);
		nativeEmployeeDue.FinanceChargeRequestId.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Transport_Dashboard_Should_Return_Summary_Alerts_And_Filter_By_Academic_Medium()
	{
		var mediumId = Factory.SeededMediumId;
		var otherMediumId = await GetOtherMediumIdAsync(mediumId);
		var setup = await GetVehicleSetupAsync(mediumId);
		var stop = (await CreateStopAsync(mediumId, UniqueName("Dashboard Stop"), "Ops")).Data!;
		var assignedRoute = (await CreateRouteAsync(mediumId, UniqueName("Dashboard Assigned"))).Data!;
		var unassignedRoute = (await CreateRouteAsync(mediumId, UniqueName("Dashboard Open"))).Data!;
		await AssignRouteStopsAsync(mediumId, assignedRoute.Id, stop.Id);
		var vehicle = (await CreateVehicleAsync(mediumId, setup.VehicleTypeId, setup.FuelTypeId, UniqueRegistrationNo())).Data!;
		var driverEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardDriver");
		var driver = (await CreateTransportStaffAsync(mediumId, driverEmployee.Id, TransportStaffRole.Driver, "DL-DASH-001")).Data!;

		var assignmentResponse = await Client.PostAsJsonAsync(
			$"/api/transport/routes/{assignedRoute.Id}/vehicle-assignment?academicMediumId={mediumId}",
			new AssignTransportRouteVehicleDto
			{
				VehicleId = vehicle.Id,
				DriverStaffId = driver.Id,
				EffectiveFrom = DateTime.UtcNow.Date,
				Remarks = "Dashboard regression assignment"
			},
			JsonOptions);
		assignmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var summary = await GetAndReadAsync<Result<TransportDashboardSummaryDto>>(
			$"/api/transport/dashboard/summary?academicMediumId={mediumId}&academicYearId={Factory.SeededAcademicYearId}");
		summary.Data.ShouldNotBeNull();
		summary.Data!.ActiveStops.ShouldBeGreaterThanOrEqualTo(1);
		summary.Data.ActiveRoutes.ShouldBeGreaterThanOrEqualTo(2);
		summary.Data.ActiveVehicles.ShouldBeGreaterThanOrEqualTo(1);
		summary.Data.AssignedRoutes.ShouldBeGreaterThanOrEqualTo(1);
		summary.Data.UnassignedRoutes.ShouldBeGreaterThanOrEqualTo(1);
		summary.Data.RouteCapacity.ShouldBeGreaterThanOrEqualTo(vehicle.SeatingCapacity);
		summary.Data.RouteCapacityUsage.ShouldContain(row => row.RouteId == assignedRoute.Id && row.HasVehicleAssignment);

		var alerts = await GetAndReadAsync<Result<PagedResultDto<TransportDashboardAlertDto>>>(
			$"/api/transport/dashboard/alerts?academicMediumId={mediumId}&academicYearId={Factory.SeededAcademicYearId}&category=Assignment&pageNumber=1&pageSize=20");
		alerts.Data.ShouldNotBeNull();
		alerts.Data!.Items.ShouldContain(row => row.EntityId == unassignedRoute.Id && row.Severity == "High");

		var isolatedSummary = await GetAndReadAsync<Result<TransportDashboardSummaryDto>>(
			$"/api/transport/dashboard/summary?academicMediumId={otherMediumId}&academicYearId={Factory.SeededAcademicYearId}");
		isolatedSummary.Data.ShouldNotBeNull();
		isolatedSummary.Data!.RouteCapacityUsage.ShouldNotContain(row => row.RouteId == assignedRoute.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Transport_Reports_Should_Return_Catalog_Rows_Csv_And_Unsupported_State()
	{
		var mediumId = Factory.SeededMediumId;
		var stop = (await CreateStopAsync(mediumId, UniqueName("Report Stop"), "Reports")).Data!;
		var route = (await CreateRouteAsync(mediumId, UniqueName("Report Route"))).Data!;
		await AssignRouteStopsAsync(mediumId, route.Id, stop.Id);

		var catalog = await GetAndReadAsync<Result<List<TransportReportCatalogItemDto>>>(
			$"/api/transport/reports/catalog?academicMediumId={mediumId}");
		catalog.Data.ShouldNotBeNull();
		catalog.Data!.ShouldContain(row => row.Key == "route-master" && row.IsSupported);
		catalog.Data.ShouldContain(row => row.Key == "gps-tracking" && !row.IsSupported);

		var routeReport = await GetAndReadAsync<Result<TransportReportResultDto>>(
			$"/api/transport/reports/route-master?academicMediumId={mediumId}&academicYearId={Factory.SeededAcademicYearId}&pageNumber=1&pageSize=20");
		routeReport.Data.ShouldNotBeNull();
		routeReport.Data!.IsSupported.ShouldBeTrue();
		routeReport.Data.Columns.ShouldContain(column => column.Key == "routeCode");
		routeReport.Data.Items.Any(row =>
			row.TryGetValue("routeCode", out var routeCode) &&
			routeCode == route.RouteCode).ShouldBeTrue();

		var export = await Client.GetAsync(
			$"/api/transport/reports/route-master/export?academicMediumId={mediumId}&academicYearId={Factory.SeededAcademicYearId}&format=csv");
		export.StatusCode.ShouldBe(HttpStatusCode.OK);
		export.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
		var csv = await export.Content.ReadAsStringAsync();
		csv.ShouldContain("Route Code");
		csv.ShouldContain(route.RouteCode);

		var unsupported = await GetAndReadAsync<Result<TransportReportResultDto>>(
			$"/api/transport/reports/gps-tracking?academicMediumId={mediumId}&academicYearId={Factory.SeededAcademicYearId}");
		unsupported.Data.ShouldNotBeNull();
		unsupported.Data!.IsSupported.ShouldBeFalse();
		unsupported.Data.Message.ShouldBe("This report depends on a future transport phase.");

		var invalidAiPlan = await Client.PostAsJsonAsync(
			"/api/transport/reports/ai/plan",
			new TransportAiReportPlanRequestDto
			{
				Prompt = "short",
				AcademicMediumId = mediumId,
				AcademicYearId = Factory.SeededAcademicYearId
			});
		invalidAiPlan.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task AssignRouteStopsAsync(Guid mediumId, Guid routeId, params Guid[] stopIds)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/transport/routes/{routeId}/stops?academicMediumId={mediumId}",
			new AssignTransportRouteStopsDto
			{
				Stops = stopIds
					.Select((stopId, index) => new TransportRouteStopInputDto
					{
						StopId = stopId,
						Order = index + 1,
						ExpectedArrivalTime = DateTimeOffset.UtcNow.Date.AddHours(7).AddMinutes(10 + (index * 10)),
						ExpectedDepartureTime = DateTimeOffset.UtcNow.Date.AddHours(7).AddMinutes(15 + (index * 10))
					})
					.ToList()
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private async Task<TransportApplicationDto> CreateTransportApplicationAsync(
		Guid mediumId,
		CreateUpdateTransportApplicationDto input)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/transport/applications?academicMediumId={mediumId}",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportApplicationDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body.Data!;
	}

	private async Task<TransportApplicationDto> AssignApplicationRouteStopAsync(Guid mediumId, Guid applicationId, Guid routeId, Guid stopId)
	{
		var response = await Client.PutAsJsonAsync(
			$"/api/transport/applications/{applicationId}/route-stop?academicMediumId={mediumId}",
			new AssignTransportApplicationRouteStopDto
			{
				RouteId = routeId,
				StopId = stopId
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportApplicationDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body.Data!;
	}

	private async Task<TransportApplicationDto> ChangeApplicationStatusAsync(
		Guid mediumId,
		Guid applicationId,
		TransportApplicationStatus status,
		string remarks,
		DateTime? effectiveDate = null)
	{
		var response = await Client.PutAsJsonAsync(
			$"/api/transport/applications/{applicationId}/status?academicMediumId={mediumId}",
			new ChangeTransportApplicationStatusDto
			{
				Status = status,
				EffectiveDate = effectiveDate,
				Remarks = remarks
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportApplicationDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body.Data!;
	}

	private async Task<Guid> CreateChargeCategoryAsync(bool isApprovalRequired = true)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/charge-categories?academicYearId={Factory.SeededAcademicYearId}",
			new CreateChargeCategoryRequest
			{
				Name = UniqueName("Transport Charge"),
				IsApprovalRequired = isApprovalRequired,
				Description = "Transport billing regression category"
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeCategoryResponse>(JsonOptions);
		body.ShouldNotBeNull();
		return body!.Id;
	}

	private async Task<TransportFeeConfigurationDto> EnsureActiveFeeConfigurationAsync(Guid mediumId, Guid chargeCategoryId)
	{
		var existing = await GetAndReadAsync<Result<TransportFeeConfigurationDto?>>(
			$"/api/transport/fee-configuration?academicMediumId={mediumId}");
		var input = new CreateUpdateTransportFeeConfigurationDto
		{
			ModelType = TransportFeeModelType.StopBased,
			FeeFrequency = TransportFeeFrequency.Monthly,
			ChargeCategoryId = chargeCategoryId,
			EffectiveFrom = DateTime.UtcNow.Date,
			IsActive = true,
			Remarks = "Active transport billing regression configuration"
		};

		HttpResponseMessage response;
		if (existing.Data == null)
		{
			response = await Client.PostAsJsonAsync(
				$"/api/transport/fee-configuration?academicMediumId={mediumId}",
				input,
				JsonOptions);
		}
		else
		{
			response = await Client.PutAsJsonAsync(
				$"/api/transport/fee-configuration/{existing.Data.Id}?academicMediumId={mediumId}",
				input,
				JsonOptions);
		}

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportFeeConfigurationDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body.Data!;
	}

	private async Task<TransportMemberDto> CreateApprovedTransportMemberAsync(
		Guid mediumId,
		TransportMemberType memberType,
		Guid sourceId,
		Guid routeId,
		Guid stopId,
		DateTime effectiveDate)
	{
		var application = await CreateTransportApplicationAsync(
			mediumId,
			new CreateUpdateTransportApplicationDto
			{
				ApplicationType = TransportApplicationType.NewEnrollment,
				MemberType = memberType,
				StudentId = memberType == TransportMemberType.Student ? sourceId : (Guid?)null,
				EmployeeId = memberType == TransportMemberType.Employee ? sourceId : (Guid?)null,
				RequestedStopId = stopId,
				PickupType = "Pickup",
				Description = "Transport billing regression enrollment"
			});
		await AssignApplicationRouteStopAsync(mediumId, application.Id, routeId, stopId);
		var approved = await ChangeApplicationStatusAsync(
			mediumId,
			application.Id,
			TransportApplicationStatus.Approved,
			"Approved for transport billing regression",
			effectiveDate);
		approved.MemberId.ShouldNotBeNull();

		var member = await GetAndReadAsync<Result<TransportMemberDto>>(
			$"/api/transport/members/{approved.MemberId}?academicMediumId={mediumId}");
		member.Data.ShouldNotBeNull();
		return member.Data!;
	}

	private async Task MoveRegistrationToEnrolledAsync(Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var student = await studentRepository.GetInquiryForUpdateAsync(studentId)
			              ?? throw new InvalidOperationException($"Student not found: {studentId}");
			student.MoveToAdmission();
			student.AssignDivision(Factory.SeededDivisionId);
			student.ApproveAdmission();

			await studentRepository.UpdateAsync(student);
			await uow.CompleteAsync();
		}
	}

	private async Task<Result<TransportStopDto>> CreateStopAsync(Guid mediumId, string stopName, string zone)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/transport/stops?academicMediumId={mediumId}",
			new CreateUpdateTransportStopDto
			{
				StopName = stopName,
				Landmark = "Main Road",
				ZoneId = zone,
				Amount = 250,
				ApproxKmFromSchool = 3.5m,
				PinCode = "395007"
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportStopDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body;
	}

	private async Task<Result<TransportRouteDto>> CreateRouteAsync(Guid mediumId, string routeName)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/transport/routes?academicMediumId={mediumId}",
			new CreateUpdateTransportRouteDto
			{
				RouteName = routeName,
				RouteCode = $"TR{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
				RouteType = "pickup",
				Distance = 8.5m,
				StartPoint = "Campus",
				EndPoint = "City Center",
				PickupSchedule = new TransportScheduleDto
				{
					StartTime = DateTimeOffset.UtcNow.Date.AddHours(7),
					EndTime = DateTimeOffset.UtcNow.Date.AddHours(8)
				}
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportRouteDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body;
	}

	private async Task<(Guid VehicleTypeId, Guid FuelTypeId)> GetVehicleSetupAsync(Guid mediumId)
	{
		var vehicleTypes = await GetAndReadAsync<Result<List<TransportVehicleTypeDto>>>(
			$"/api/transport/vehicle-types?academicMediumId={mediumId}");
		var fuelTypes = await GetAndReadAsync<Result<List<TransportFuelTypeDto>>>(
			$"/api/transport/fuel-types?academicMediumId={mediumId}");

		vehicleTypes.Data.ShouldNotBeNull();
		fuelTypes.Data.ShouldNotBeNull();
		return (vehicleTypes.Data!.First().Id, fuelTypes.Data!.First().Id);
	}

	private async Task<Result<TransportVehicleDto>> CreateVehicleAsync(Guid mediumId, Guid vehicleTypeId, Guid fuelTypeId, string registrationNo)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/transport/vehicles?academicMediumId={mediumId}",
			BuildVehicleInput(vehicleTypeId, fuelTypeId, registrationNo),
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportVehicleDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body;
	}

	private async Task<Result<TransportStaffDto>> CreateTransportStaffAsync(
		Guid mediumId,
		Guid employeeId,
		TransportStaffRole role,
		string? licenseNo = null)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/transport/staff?academicMediumId={mediumId}",
			new CreateUpdateTransportStaffDto
			{
				EmployeeId = employeeId,
				Role = role,
				LicenseNo = role == TransportStaffRole.Driver ? licenseNo ?? $"DL-{Guid.NewGuid():N}"[..12].ToUpperInvariant() : licenseNo,
				LicenseExpiryDate = role == TransportStaffRole.Driver ? DateTime.UtcNow.Date.AddYears(3) : null,
				ExperienceYears = role == TransportStaffRole.Driver ? 5 : 2,
				Remarks = "Transport API regression staff"
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<TransportStaffDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Data.ShouldNotBeNull();
		return body;
	}

	private static CreateUpdateTransportVehicleDto BuildVehicleInput(Guid vehicleTypeId, Guid fuelTypeId, string registrationNo) => new()
	{
		RegistrationNo = registrationNo,
		VehicleNo = registrationNo.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase),
		VehicleName = "Fleet vehicle",
		VehicleTypeId = vehicleTypeId,
		VehicleCompany = "Tata",
		Model = "Starbus",
		YearOfManufacture = "2024",
		SeatingCapacity = 40,
		VehicleColor = "Yellow",
		SchoolAssignedNo = registrationNo,
		Owner = "School",
		FuelTypeId = fuelTypeId,
		IsAc = false,
		Description = "Transport API regression vehicle"
	};

	private static MultipartFormDataContent BuildDocumentUpload(Guid requiredDocumentId, string fileName, byte[] bytes, string contentType)
	{
		var content = new MultipartFormDataContent();
		content.Add(new StringContent(requiredDocumentId.ToString()), "requiredDocumentId");
		content.Add(new StringContent(fileName), "fileName");
		var fileContent = new ByteArrayContent(bytes);
		fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		content.Add(fileContent, "file", fileName);
		return content;
	}

	private static MultipartFormDataContent BuildMaintenanceUpload(string serviceType, DateTime serviceDate, byte[] photoBytes)
	{
		var content = new MultipartFormDataContent();
		content.Add(new StringContent(serviceType), "serviceType");
		content.Add(new StringContent(serviceDate.ToString("O")), "serviceDate");
		content.Add(new StringContent(serviceDate.AddMonths(6).ToString("O")), "nextServiceDate");
		content.Add(new StringContent("1200"), "cost");
		content.Add(new StringContent("Campus Garage"), "provider");
		content.Add(new StringContent("Routine service"), "notes");
		var photoContent = new ByteArrayContent(photoBytes);
		photoContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		content.Add(photoContent, "photos", "maintenance.png");
		return content;
	}

	private async Task<Guid> GetOtherMediumIdAsync(Guid currentMediumId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await repository.GetQueryableAsync();
			var otherMedium = await query.AsNoTracking().FirstOrDefaultAsync(medium => medium.Id != currentMediumId);
			otherMedium.ShouldNotBeNull("Transport academic-medium isolation test needs at least two seeded education mediums.");
			await uow.CompleteAsync();
			return otherMedium!.Id;
		}
	}

	private static string UniqueName(string prefix)
	{
		return $"{prefix} {Guid.NewGuid():N}"[..30];
	}

	private static string UniqueRegistrationNo()
	{
		return $"GJ05{Guid.NewGuid():N}"[..12].ToUpperInvariant();
	}
}
