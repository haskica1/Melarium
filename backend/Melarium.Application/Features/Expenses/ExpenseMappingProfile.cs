using AutoMapper;
using Melarium.Application.Features.Expenses.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Expenses;

public class ExpenseMappingProfile : AutoMapper.Profile
{
    public ExpenseMappingProfile()
    {
        CreateMap<Expense, ExpenseDto>()
            .ForMember(d => d.SourceName, o => o.MapFrom(s => s.Source.ToString()))
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count))
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s =>
                s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null));

        CreateMap<Expense, ExpenseDetailDto>()
            .ForMember(d => d.SourceName, o => o.MapFrom(s => s.Source.ToString()))
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count))
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s =>
                s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null));

        CreateMap<ExpenseItem, ExpenseItemDto>();

        CreateMap<CreateExpenseDto, Expense>()
            .ForMember(d => d.Items, o => o.MapFrom(s => s.Items));

        CreateMap<UpdateExpenseDto, Expense>()
            .ForMember(d => d.Source, o => o.Ignore())   // source is immutable after creation
            .ForMember(d => d.OrganizationId, o => o.Ignore())
            .ForMember(d => d.CreatedById, o => o.Ignore())
            .ForMember(d => d.Items, o => o.MapFrom(s => s.Items));

        CreateMap<CreateExpenseItemDto, ExpenseItem>();
    }
}
