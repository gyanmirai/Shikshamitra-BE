using System;
using System.Collections.Generic;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Library.Shared;

public sealed record LibraryAcademicContext(UserScopeContext UserScope, List<Guid> AllowedAcademicYearIds);
