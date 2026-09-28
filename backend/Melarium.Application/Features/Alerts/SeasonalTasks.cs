using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Alerts;

/// <summary>
/// The short list of work that opens each season phase (SPEC-29), sent once per phase as
/// <c>SeasonPhaseStarted</c>. Kept in code for the same reason help content is (ADR-031): it changes
/// rarely, in the same deploy as the rules around it, and needs no editor. Long-form articles on the
/// same subjects live in Edukacija, which the notice links to.
/// </summary>
public static class SeasonalTasks
{
    public static IReadOnlyList<string> For(SeasonPhase phase) => phase switch
    {
        SeasonPhase.Winter =>
        [
            "Ne otvarajte košnice — stanje pratite po zvuku i po podnjači",
            "Zimski tretman protiv varoe (oksalna kiselina) u bezleglom periodu",
            "Povremeno očistite leto od snijega i mrtvih pčela, provjerite zaštitu od miševa",
            "Pripremite okvire, satne osnove i opremu za proljeće",
        ],
        SeasonPhase.SpringBuildUp =>
        [
            "Provjerite zalihe hrane — krajem zime društva najčešće gladuju",
            "Prvi proljetni pregled na toplom danu (iznad 12–15 °C): matica, leglo, hrana",
            "Po potrebi pogače ili stimulativna prihrana",
            "Očistite podnjače, pojačajte ili spojite slaba društva",
        ],
        SeasonPhase.MainSeason =>
        [
            "Pregledi svakih 7–10 dana u rojevom periodu",
            "Na vrijeme dodajte prostor — nastavke i satne osnove",
            "Mjere protiv rojenja",
            "Vrcanje zrelog, poklopljenog meda — pazite na karencu nakon tretmana",
        ],
        SeasonPhase.LateSummer =>
        [
            "Tretman protiv varoe odmah nakon posljednjeg vrcanja",
            "Procjena zaliha i prihrana za zimu",
            "Zamjena starih ili slabih matica",
            "Spajanje slabih društava",
        ],
        SeasonPhase.Wintering =>
        [
            "Završna provjera zaliha hrane za zimu",
            "Suženje leta i zaštita od miševa",
            "Utopljavanje i provjetravanje košnica",
            "Posljednji pregled prije mirovanja",
        ],
        _ => [],
    };
}
