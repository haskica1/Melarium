using Melarium.Application.Common.Validation;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the canonical form of a phone number. <c>User.Phone</c> is a unique key and a login
/// identifier, so two spellings of one number collapsing to one value is the property everything
/// else depends on: get it wrong and the same person can register twice, or sign up in one
/// notation and fail to sign in with another.
/// </summary>
public class PhoneRulesTests
{
    [Theory]
    // Local notation, as a BiH user would write their own number.
    [InlineData("061123456")]
    [InlineData("061 123 456")]
    [InlineData("061-123-456")]
    [InlineData("061/123-456")]
    [InlineData("(061) 123 456")]
    // International notation for the same number.
    [InlineData("+38761123456")]
    [InlineData("+387 61 123 456")]
    [InlineData("0038761123456")]
    [InlineData("00 387 61 123 456")]
    // Country code without a prefix — no national number starts with 387.
    [InlineData("38761123456")]
    // Local number with the trunk zero already dropped.
    [InlineData("61123456")]
    [InlineData("  061 123 456  ")]
    public void Normalize_collapses_every_spelling_to_one_canonical_value(string typed) =>
        Assert.Equal("+38761123456", PhoneRules.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("061")]              // too short to be a number
    [InlineData("+0611234567")]      // country code cannot start with 0
    [InlineData("06112345678901234")] // past E.164's 15 digits
    public void Normalize_rejects_what_cannot_be_a_number(string? typed) =>
        Assert.Null(PhoneRules.Normalize(typed));

    // Foreign numbers are not rewritten — only local notation gets the +387 assumption.
    [Fact]
    public void Normalize_keeps_an_explicit_foreign_country_code()
    {
        Assert.Equal("+4915112345678", PhoneRules.Normalize("+49 151 12345678"));
        Assert.Equal("+38598123456", PhoneRules.Normalize("0038598123456"));
    }

    [Fact]
    public void Normalize_is_idempotent() =>
        Assert.Equal("+38761123456", PhoneRules.Normalize(PhoneRules.Normalize("061 123 456")));
}
