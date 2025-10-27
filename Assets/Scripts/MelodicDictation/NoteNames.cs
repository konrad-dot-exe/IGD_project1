public static class NoteNames
{
    // Combined enharmonics per your spec (key-aware spelling can be added later)
    static readonly string[] PCS = {
        "C", "C#/Db", "D", "D#/Eb", "E", "F",
        "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B"
    };

    public static string NameForMidi(int midi)
    {
        if (midi < 0) midi = 0;
        int pc = ((midi % 12) + 12) % 12;
        int octave = (midi / 12) - 1;
        return $"{PCS[pc]}{octave}";
    }
}
