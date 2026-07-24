using AutoMapper;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Beehives.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Beehives;

public class BeehiveMappingProfile : AutoMapper.Profile
{
    public BeehiveMappingProfile()
    {
        // *Name fields are UI-facing — Bosnian labels, same source as Stats (BsLabels).
        CreateMap<Beehive, BeehiveDto>()
            .ForMember(d => d.TypeName, o => o.MapFrom(s => BsLabels.Label(s.Type)))
            .ForMember(d => d.MaterialName, o => o.MapFrom(s => BsLabels.Label(s.Material)))
            .ForMember(d => d.InspectionCount, o => o.MapFrom(s => s.Inspections.Count))
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s =>
                s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null));

        CreateMap<Beehive, BeehiveDetailDto>()
            .ForMember(d => d.TypeName, o => o.MapFrom(s => BsLabels.Label(s.Type)))
            .ForMember(d => d.MaterialName, o => o.MapFrom(s => BsLabels.Label(s.Material)))
            .ForMember(d => d.InspectionCount, o => o.MapFrom(s => s.Inspections.Count()))
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s =>
                s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null));

        CreateMap<CreateBeehiveDto, Beehive>();
        CreateMap<UpdateBeehiveDto, Beehive>();
    }
}
