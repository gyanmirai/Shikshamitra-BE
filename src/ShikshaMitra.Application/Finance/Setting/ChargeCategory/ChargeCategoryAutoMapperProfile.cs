using AutoMapper;
using ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Finance.Setting.ChargeCategory;

namespace ShikshaMitra.Application.Finance.Setting.ChargeCategory;

public class ChargeCategoryAutoMapperProfile : Profile
{
    public ChargeCategoryAutoMapperProfile()
    {
        CreateMap<ShikshaMitra.Finance.Setting.ChargeCategory.ChargeCategory, ChargeCategoryResponse>();
    }
}
