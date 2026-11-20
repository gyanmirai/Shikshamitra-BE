using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Charges;

public sealed record LibraryChargePositionReadInput(LibraryChargePositionReadPurpose Purpose,
	Guid? AcademicYearId = null, IReadOnlyCollection<Guid>? AssessmentIds = null);
