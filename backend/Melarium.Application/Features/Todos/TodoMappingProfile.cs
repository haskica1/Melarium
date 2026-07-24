using AutoMapper;
using Melarium.Application.Features.Todos.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Todos;

public class TodoMappingProfile : AutoMapper.Profile
{
    public TodoMappingProfile()
    {
        CreateMap<Todo, TodoDto>()
            .ForMember(d => d.PriorityName, o => o.MapFrom(s => s.Priority.ToString()))
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s =>
                s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null))
            .ForMember(d => d.AssignedToName, o => o.MapFrom(s =>
                s.AssignedTo != null ? $"{s.AssignedTo.FirstName} {s.AssignedTo.LastName}" : null));

        CreateMap<CreateTodoDto, Todo>()
            .ForMember(d => d.AssignedTo, o => o.Ignore()); // navigation loaded separately
        CreateMap<UpdateTodoDto, Todo>()
            .ForMember(d => d.CompletedAt, o => o.Ignore()) // managed in service
            .ForMember(d => d.AssignedTo, o => o.Ignore());
    }
}
