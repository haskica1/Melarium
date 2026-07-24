using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>
/// International queen-marking color code: the last digit of the birth year determines
/// the color (1/6 white, 2/7 yellow, 3/8 red, 4/9 green, 5/0 blue).
/// </summary>
public static class QueenMarkColorHelper
{
    public static QueenMarkColor ForYear(int year) => (year % 10) switch
    {
        1 or 6 => QueenMarkColor.White,
        2 or 7 => QueenMarkColor.Yellow,
        3 or 8 => QueenMarkColor.Red,
        4 or 9 => QueenMarkColor.Green,
        _      => QueenMarkColor.Blue, // years ending in 5 or 0
    };
}
