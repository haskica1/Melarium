using Melarium.Application.Common.Exceptions;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Todos.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Manages to-do items attached to an apiary or a beehive. Access is enforced in the service layer:
/// apiary todos require management rights; hive todos follow hive access (managers or assigned Beekeeper).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class TodosController : ControllerBase
{
    private readonly ITodoService _service;
    private readonly IValidator<CreateTodoDto> _createValidator;
    private readonly IValidator<UpdateTodoDto> _updateValidator;

    public TodosController(
        ITodoService service,
        IValidator<CreateTodoDto> createValidator,
        IValidator<UpdateTodoDto> updateValidator)
    {
        _service         = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Returns all to-do items for the given apiary, open items first.</summary>
    [HttpGet("by-apiary/{apiaryId:int}")]
    [ProducesResponseType(typeof(IEnumerable<TodoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByApiary(int apiaryId)
    {
        var todos = await _service.GetByApiaryIdAsync(apiaryId);
        return Ok(todos);
    }

    /// <summary>Returns all to-do items for the given beehive, open items first.</summary>
    [HttpGet("by-beehive/{beehiveId:int}")]
    [ProducesResponseType(typeof(IEnumerable<TodoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBeehive(int beehiveId)
    {
        var todos = await _service.GetByBeehiveIdAsync(beehiveId);
        return Ok(todos);
    }

    /// <summary>Returns all open (non-completed) to-do items accessible to the current user (role-scoped). Used by global search.</summary>
    [HttpGet("all-open")]
    [ProducesResponseType(typeof(IEnumerable<TodoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllOpen()
    {
        var todos = await _service.GetAllOpenForCurrentUserAsync();
        return Ok(todos);
    }

    /// <summary>Returns a single to-do item by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TodoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var todo = await _service.GetByIdAsync(id);
        return Ok(todo);
    }

    /// <summary>Creates a new to-do item for an apiary or a beehive the caller can manage/access.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TodoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateTodoDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a to-do item.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TodoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTodoDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes a to-do item.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Returns users that can be assigned a todo for a beehive the caller can access.
    /// Returns an empty list instead of an error when the beehive does not exist or the
    /// caller has no access — the client should simply hide the assignee control.
    /// </summary>
    [HttpGet("assignable-users/{beehiveId:int}")]
    [ProducesResponseType(typeof(IEnumerable<AssignableUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignableUsersForBeehive(int beehiveId)
    {
        try
        {
            var users = await _service.GetAssignableUsersForBeehiveAsync(beehiveId);
            return Ok(users);
        }
        catch (NotFoundException)
        {
            return Ok(Array.Empty<AssignableUserDto>());
        }
        catch (ForbiddenAccessException)
        {
            return Ok(Array.Empty<AssignableUserDto>());
        }
    }
}
