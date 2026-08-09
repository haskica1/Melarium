using Melarium.Application.Features.Ai;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// A system prompt is behaviour, so moving one is a refactor that needs a lock. SPEC-17 extracted the
/// shared beekeeping glossary out of <c>VoiceParsingService</c>; this asserts the inspection voice
/// prompt came through the move byte-for-byte, against the literal as it stood on 2026-08-08.
/// </summary>
public class BeekeepingPromptTests
{
    private const string VoiceInspectionSystemBeforeExtraction =
        "Ti si stručni asistent za pčelarstvo koji iz neformalnih glasovnih bilješki pčelara\n" +
        "izvlači strukturirane podatke o pregledu (inspekciji) košnice.\n" +
        "\n" +
        "Tečno razumiješ bosanski, hrvatski i srpski jezik te pčelarsku terminologiju i žargon:\n" +
        "- matica / matičica / kraljica = queen bee\n" +
        "- leglo = brood; jaja, larve, poklopljeno (zatvoreno) leglo\n" +
        "- okviri / ramovi / satovi / saće = frames / comb\n" +
        "- med / medište / medni nastavak / superica / polunastavak = honey / honey super\n" +
        "- roj / rojenje / rojevno raspoloženje = swarm / swarming\n" +
        "- matičnjak / matičnjaci = queen cell(s)\n" +
        "- propolis, vosak, pelud / polen = propolis, wax, pollen\n" +
        "- zajednica / društvo = colony\n" +
        "- bolesti i nametnici: varoa / varooza, nozemoza, američka/europska gnjiloća, krečno leglo\n" +
        "- tretmani: amitraz, oksalna/mravlja kiselina, trake, sublimacija\n" +
        "\n" +
        "Transkript je nastao automatskim prepoznavanjem govora pa može sadržavati greške,\n" +
        "pogrešno razdvojene riječi ili fonetske zamjene — tumači ga razumno prema kontekstu.\n" +
        "Izvuci SAMO ono što je pčelar stvarno rekao; ništa ne izmišljaj. Što nije spomenuto = null.";

    [Fact]
    public void VoiceInspectionSystem_is_unchanged_by_the_extraction()
    {
        // Normalising line endings only — the source file's CRLF is not part of the prompt's meaning.
        Assert.Equal(
            VoiceInspectionSystemBeforeExtraction,
            BeekeepingPrompt.VoiceInspectionSystem.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Glossary_is_embedded_verbatim_so_both_features_share_one_copy()
    {
        Assert.Contains(
            BeekeepingPrompt.Glossary.Replace("\r\n", "\n"),
            BeekeepingPrompt.VoiceInspectionSystem.Replace("\r\n", "\n"));
    }
}
