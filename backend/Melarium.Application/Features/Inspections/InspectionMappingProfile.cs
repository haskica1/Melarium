using AutoMapper;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Inspections;

public class InspectionMappingProfile : AutoMapper.Profile
{
    public InspectionMappingProfile()
    {
        CreateMap<Inspection, InspectionDto>()
            .ForMember(d => d.HoneyLevelName, o => o.MapFrom(s => s.HoneyLevel.ToString()));

        CreateMap<CreateInspectionDto, Inspection>();
        CreateMap<UpdateInspectionDto, Inspection>();

        CreateMap<InspectionPhoto, InspectionPhotoDto>();
    }
}
