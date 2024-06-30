using System;
using System.Collections.Generic;
using System.Text;
using ShikshaMitra.Localization;
using Volo.Abp.Application.Services;

namespace ShikshaMitra;

/* Inherit your application services from this class.
 */
public abstract class ShikshaMitraAppService : ApplicationService
{
    protected ShikshaMitraAppService()
    {
        LocalizationResource = typeof(ShikshaMitraResource);
    }
}
