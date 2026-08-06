using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Expenses;
using Melarium.Application.Features.Expenses.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the SPEC-12 Phase E attribution rules. Uses a real <see cref="ExpenseMappingProfile"/>
/// (not a mocked IMapper) because the whole point of these tests is whether DietId actually survives
/// AutoMapper's convention-based mapping through UpdateAsync's Items.Clear()-then-remap — the single
/// most likely bug in that phase.
/// </summary>
public class ExpenseServiceTests
{
    private const int Apiary = 3;
    private const int OrgId = 7;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<ExpenseMappingProfile>()).CreateMapper();
    private readonly ExpenseService _service;

    public ExpenseServiceTests()
    {
        _service = new ExpenseService(
            _uow, _mapper,
            new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = OrgId });
    }

    private static Diet DietIn(int apiaryId, int id = 50) => new() { Id = id, ApiaryId = apiaryId, Name = "Zimska prehrana" };

    /// <summary>Wires a diet lookup and its apiary's organization. Pass a different orgId to simulate
    /// a cross-organization attribution attempt.</summary>
    private void WireDiet(Diet diet, int apiaryOrgId = OrgId)
    {
        _uow.Diets.GetByIdAsync(diet.Id).Returns(diet);
        _uow.Apiaries.GetByIdAsync(diet.ApiaryId).Returns(new Apiary { Id = diet.ApiaryId, OrganizationId = apiaryOrgId });
    }

    private static CreateExpenseItemDto Item(string name = "Šećer", int? dietId = null) => new()
    {
        Name = name, Quantity = 1, UnitPrice = 10, TotalPrice = 10, DietId = dietId,
    };

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_AttributesItemToDiet_WhenDietBelongsToCallersOrg()
    {
        var diet = DietIn(Apiary);
        WireDiet(diet);
        Expense? saved = null;
        _uow.Expenses.AddAsync(Arg.Do<Expense>(e => saved = e)).Returns(ci => ci.Arg<Expense>());
        _uow.Expenses.GetWithItemsAsync(Arg.Any<int>()).Returns(_ => saved);

        await _service.CreateAsync(new CreateExpenseDto
        {
            PurchaseDate = DateTime.UtcNow,
            Currency     = "BAM",
            Items        = [Item(dietId: diet.Id)],
        });

        Assert.NotNull(saved);
        Assert.Equal(diet.Id, saved!.Items.Single().DietId);
    }

    [Fact]
    public async Task Create_UnattributedItem_LeavesDietIdNull()
    {
        Expense? saved = null;
        _uow.Expenses.AddAsync(Arg.Do<Expense>(e => saved = e)).Returns(ci => ci.Arg<Expense>());
        _uow.Expenses.GetWithItemsAsync(Arg.Any<int>()).Returns(_ => saved);

        await _service.CreateAsync(new CreateExpenseDto
        {
            PurchaseDate = DateTime.UtcNow,
            Currency     = "BAM",
            Items        = [Item()],
        });

        Assert.Null(saved!.Items.Single().DietId);
        // No diet was referenced, so the boundary check must not even look one up.
        await _uow.Diets.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task Create_UnknownDiet_ThrowsNotFound()
    {
        _uow.Diets.GetByIdAsync(99).Returns((Diet?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(new CreateExpenseDto
            {
                PurchaseDate = DateTime.UtcNow,
                Currency     = "BAM",
                Items        = [Item(dietId: 99)],
            }));
    }

    [Fact]
    public async Task Create_DietOfAnotherOrganization_ThrowsValidationNot403Or404()
    {
        var diet = DietIn(Apiary);
        WireDiet(diet, apiaryOrgId: 999); // a different organization than the caller's

        // ValidationException (400): the id is well-formed and does exist, it is just not
        // attributable — distinct from NotFoundException (404) and ForbiddenAccessException (403).
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new CreateExpenseDto
            {
                PurchaseDate = DateTime.UtcNow,
                Currency     = "BAM",
                Items        = [Item(dietId: diet.Id)],
            }));
    }

    // ── UpdateAsync — the Items.Clear() trap ──────────────────────────────────

    [Fact]
    public async Task Update_EditingAnUnrelatedField_PreservesExistingDietAttribution()
    {
        var diet = DietIn(Apiary);
        WireDiet(diet);

        var existing = new Expense
        {
            Id = 1, OrganizationId = OrgId, Currency = "BAM", PurchaseDate = DateTime.UtcNow,
            Items =
            [
                new ExpenseItem { Id = 100, Name = "Šećer", Quantity = 1, UnitPrice = 10, TotalPrice = 10, DietId = diet.Id, SortOrder = 0 },
            ],
        };
        _uow.Expenses.GetWithItemsAsync(1).Returns(existing);

        // Only the note changes on this edit — the item payload still carries the SAME dietId,
        // exactly as ExpenseFormPage.tsx is required to resend it on every submit.
        await _service.UpdateAsync(1, new UpdateExpenseDto
        {
            PurchaseDate = existing.PurchaseDate,
            Currency     = "BAM",
            Notes        = "ažurirana napomena",
            Items        = [Item(dietId: diet.Id)],
        });

        Assert.Equal(diet.Id, existing.Items.Single().DietId);
    }

    [Fact]
    public async Task Update_ItemPayloadOmitsDietId_ClearsAttribution()
    {
        // This documents the trap itself, not a bug: Items.Clear() + remap means whatever the
        // payload carries becomes the new truth — there is no merge with what was there before.
        // It is why the frontend form's every touch point (defaults, reset, submit) must resend it.
        var existing = new Expense
        {
            Id = 1, OrganizationId = OrgId, Currency = "BAM", PurchaseDate = DateTime.UtcNow,
            Items =
            [
                new ExpenseItem { Id = 100, Name = "Šećer", Quantity = 1, UnitPrice = 10, TotalPrice = 10, DietId = 50, SortOrder = 0 },
            ],
        };
        _uow.Expenses.GetWithItemsAsync(1).Returns(existing);

        await _service.UpdateAsync(1, new UpdateExpenseDto
        {
            PurchaseDate = existing.PurchaseDate,
            Currency     = "BAM",
            Items        = [Item(dietId: null)],
        });

        Assert.Null(existing.Items.Single().DietId);
    }

    [Fact]
    public async Task Update_DietOfAnotherOrganization_ThrowsValidation()
    {
        var diet = DietIn(Apiary);
        WireDiet(diet, apiaryOrgId: 999);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(1, new UpdateExpenseDto
            {
                PurchaseDate = DateTime.UtcNow,
                Currency     = "BAM",
                Items        = [Item(dietId: diet.Id)],
            }));

        // Rejected before even loading the expense — the boundary check runs first.
        await _uow.Expenses.DidNotReceive().GetWithItemsAsync(Arg.Any<int>());
    }
}
