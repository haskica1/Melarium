namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq prompt text shared by the features that have to read a beekeeper's own words. The glossary
/// was extracted from <c>VoiceParsingService</c> when SPEC-17 needed the same tuning: two copies of a
/// hand-tuned prompt drift apart, and the drift is invisible until output quality quietly drops.
/// The composed messages live here too so a test can lock them — a prompt is behaviour.
/// </summary>
public static class BeekeepingPrompt
{
    /// <summary>BCS beekeeping terminology and slang. Shared verbatim by every prompt below.</summary>
    public const string Glossary =
        """
        Tečno razumiješ bosanski, hrvatski i srpski jezik te pčelarsku terminologiju i žargon:
        - matica / matičica / kraljica = queen bee
        - leglo = brood; jaja, larve, poklopljeno (zatvoreno) leglo
        - okviri / ramovi / satovi / saće = frames / comb
        - med / medište / medni nastavak / superica / polunastavak = honey / honey super
        - roj / rojenje / rojevno raspoloženje = swarm / swarming
        - matičnjak / matičnjaci = queen cell(s)
        - propolis, vosak, pelud / polen = propolis, wax, pollen
        - zajednica / društvo = colony
        - bolesti i nametnici: varoa / varooza, nozemoza, američka/europska gnjiloća, krečno leglo
        - tretmani: amitraz, oksalna/mravlja kiselina, trake, sublimacija
        """;

    /// <summary>
    /// System message for inspection voice parsing (SPEC-01). Byte-identical to the literal that
    /// lived inside <c>VoiceParsingService</c> before the extraction — locked by
    /// <c>BeekeepingPromptTests</c> so the move stays a move and not a retune.
    /// </summary>
    public const string VoiceInspectionSystem =
        $"""
        Ti si stručni asistent za pčelarstvo koji iz neformalnih glasovnih bilješki pčelara
        izvlači strukturirane podatke o pregledu (inspekciji) košnice.

        {Glossary}

        Transkript je nastao automatskim prepoznavanjem govora pa može sadržavati greške,
        pogrešno razdvojene riječi ili fonetske zamjene — tumači ga razumno prema kontekstu.
        Izvuci SAMO ono što je pčelar stvarno rekao; ništa ne izmišljaj. Što nije spomenuto = null.
        """;
}
