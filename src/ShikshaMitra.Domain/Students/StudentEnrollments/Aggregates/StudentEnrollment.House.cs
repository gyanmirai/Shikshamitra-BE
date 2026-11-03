using System;
using ShikshaMitra.Students.Records;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments;

public partial class StudentEnrollment
{
	public Guid? HouseId { get; private set; }

	public void AssignHouse(Guid? houseId)
	{
		if (!IsActive || houseId == Guid.Empty)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		HouseId = houseId;
	}
}
