namespace Melarium.Application.Features.Todos.DTOs;

/// <summary>A user that can be assigned a todo (ApiaryAdmin or assigned Beekeeper).</summary>
public record AssignableUserDto(int Id, string FullName);
