namespace ShikshaMitra.Transport;

public static class TransportErrorCodes
{
	public const string StopAlreadyExists = "TR:Stop:010001";
	public const string StopNotFound = "TR:Stop:010002";
	public const string RouteAlreadyExists = "TR:Route:020001";
	public const string RouteNotFound = "TR:Route:020002";
	public const string RouteStopInvalid = "TR:Route:020003";
	public const string VehicleTypeAlreadyExists = "TR:Setting:030001";
	public const string RequiredVehicleDocumentAlreadyExists = "TR:Setting:030002";
	public const string ServiceTypeAlreadyExists = "TR:Setting:030003";
	public const string FuelTypeAlreadyExists = "TR:Setting:030004";
	public const string VehicleAlreadyExists = "TR:Vehicle:040001";
	public const string VehicleNotFound = "TR:Vehicle:040002";
	public const string VehicleDocumentNotFound = "TR:Vehicle:040003";
	public const string RequiredVehicleDocumentNotFound = "TR:Vehicle:040004";
	public const string VehicleTypeNotFound = "TR:Vehicle:040005";
	public const string FuelTypeNotFound = "TR:Vehicle:040006";
	public const string StaffAlreadyExists = "TR:Staff:050001";
	public const string StaffNotFound = "TR:Staff:050002";
	public const string StaffEmployeeNotFound = "TR:Staff:050003";
	public const string StaffEmployeeInactive = "TR:Staff:050004";
	public const string StaffRoleInvalid = "TR:Staff:050005";
	public const string RouteAssignmentConflict = "TR:RouteAssignment:060001";
	public const string RouteAssignmentInvalid = "TR:RouteAssignment:060002";
	public const string MemberSourceInvalid = "TR:Member:070001";
	public const string MemberAlreadyExists = "TR:Member:070002";
	public const string MemberNotFound = "TR:Member:070003";
	public const string ApplicationDuplicate = "TR:Application:080001";
	public const string ApplicationInvalidStatus = "TR:Application:080002";
	public const string ApplicationRouteStopInvalid = "TR:Application:080003";
	public const string ApplicationCapacityExceeded = "TR:Application:080004";
	public const string ZoneAreaAlreadyExists = "TR:Fee:090001";
	public const string FeeConfigurationInvalid = "TR:Fee:090002";
	public const string FeeConfigurationAlreadyExists = "TR:Fee:090003";
	public const string FeeConfigurationNotFound = "TR:Fee:090004";
	public const string FeeRunAlreadyExists = "TR:Fee:090005";
	public const string FeeDueAlreadyExists = "TR:Fee:090006";
	public const string FeeRunNotFound = "TR:Fee:090007";
	public const string FeeRunHasNoDues = "TR:Fee:090008";
	public const string FeeFinancePostInvalid = "TR:Fee:090009";
}
