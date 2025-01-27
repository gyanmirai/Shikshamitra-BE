using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance;

    public class FinanceMapperProfile :Profile 
    {
	public FinanceMapperProfile()
	{
		#region Setting
		#region GeneralSetting
		#region FeeHead

		// Map from Entity to DTO
		CreateMap<FeeHead, FeeHeadDto>();

		// Map from DTO to Entity
		CreateMap<CreateFeeHeadDto, FeeHead>();



		#endregion
		#endregion
		#endregion
	}

}
