using Melarium.Application.Common.Security;
using Melarium.Application.Features.Expenses;
using Melarium.Application.Features.Expenses.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Manages expense records for an organization. Every operation is scoped to the caller's
/// organization in the service layer. Accessible to ApiaryAdmin, OrgAdmin, and SystemAdmin roles.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = Roles.Managers)]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _service;
    private readonly IValidator<CreateExpenseDto> _createValidator;
    private readonly IValidator<UpdateExpenseDto> _updateValidator;

    public ExpensesController(
        IExpenseService service,
        IValidator<CreateExpenseDto> createValidator,
        IValidator<UpdateExpenseDto> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Returns all expenses for the caller's organization, ordered by purchase date descending.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExpenseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var expenses = await _service.GetAllForCurrentOrganizationAsync();
        return Ok(expenses);
    }

    /// <summary>Returns a single expense with all its line items.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ExpenseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var expense = await _service.GetByIdAsync(id);
        return Ok(expense);
    }

    /// <summary>Creates a new expense with line items.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpenseDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing expense and replaces all its line items.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ExpenseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes an expense and all its line items.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
