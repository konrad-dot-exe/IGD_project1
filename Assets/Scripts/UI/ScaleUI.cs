using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScaleUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputScale;
    public Button btnGenerate;
    public Button btnPlay;
    public TextMeshProUGUI textStatus;
    public TextMeshProUGUI logText;         // the scrollable terminal area
    public ScrollRect logScroll;

    [Header("Logic")]
    public MusicOrchestrator orchestrator;  // NEW
    public MusicDataController dataController;
    public PlaybackController playback;

    // Add this helper inside ScaleUI (anywhere in the class)
    static NoteNamer.Mode ParseMode(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "ionian" or "major"      => NoteNamer.Mode.Ionian,
            "dorian"                 => NoteNamer.Mode.Dorian,
            "phrygian"               => NoteNamer.Mode.Phrygian,
            "lydian"                 => NoteNamer.Mode.Lydian,
            "mixolydian"             => NoteNamer.Mode.Mixolydian,
            "aeolian" or "minor"     => NoteNamer.Mode.Aeolian,
            "locrian"                => NoteNamer.Mode.Locrian,
            _                        => NoteNamer.Mode.Ionian, // safe default
        };
    }

    void Awake()
    {
        btnGenerate.onClick.AddListener(() => _ = OnGenerateAsync());
        btnPlay.onClick.AddListener(OnPlay);
        AppendLog("Session started.");
        UpdateStatus("Idle.");
    }

    async Task OnGenerateAsync()
    {
        var scale = inputScale.text.Trim();
        if (string.IsNullOrEmpty(scale)) return;

        btnGenerate.interactable = false;
        btnPlay.interactable = false;
        AppendLog($">>> generate_scale \"{scale}\"");
        UpdateStatus("Working…");

        var (ok, err) = await orchestrator.GenerateScaleAsync(scale, AppendLog);
        if (!ok)
        {
            UpdateStatus("Error. See Console.");
            AppendLog($"<color=#ff6666>[error]</color> {err}");
            btnGenerate.interactable = true;
            return;
        }

        // pretty one-line
        var noteLine = BuildNoteListLine();
        AppendLog($"<b>Notes:</b> {noteLine}");

        // technical validation confirmation
        var structural = MusicValidator.ValidateScale(dataController.Current);
        if (!structural.Ok)
        {
            AppendLog($"<color=#ffaa00>[validation]</color>\n{structural}");
        }
        else
        {
            AppendLog("<color=#66cc66>[validation]</color> OK");
        }

        var theory = TheoryValidator.Run(dataController.Current, TheoryValidator.Profiles.Scale);
        if (theory.Ok)
            AppendLog("<color=#66cc66>[theory]</color> OK");
        else
            AppendLog($"<color=#ffaa00>[theory]</color>\n{theory}");

        UpdateStatus("Ready.");
        btnGenerate.interactable = true;
        btnPlay.interactable = true;
    }

    void OnPlay()
    {
        if (dataController?.Current == null) return;
        playback?.Play(); // your existing method
    }

    // Replace your BuildNoteListLine() with:
    string BuildNoteListLine()
    {
        var md = dataController.Current;
        if (md == null || md.notes == null || md.notes.Count == 0) return "(none)";

        int rootPc = md.notes[0].pitch % 12;
        var mode = ParseMode(md.meta?.scale_kind);
        var ctx  = new NoteNamer.KeyContext(rootPc, mode);

        var names = new System.Collections.Generic.List<string>(md.notes.Count);
        foreach (var n in md.notes)
        {
            var name = NoteNamer.NameForMidi(n.pitch, ctx);
            var oct  = NoteNamer.OctaveOfMidi(n.pitch);
            names.Add($"{name}{oct}");
        }
        return string.Join(" ", names);
    }

    void AppendLog(string line)
    {
        if (!logText) return;
        logText.text += (logText.text.Length > 0 ? "\n" : "") + line;
        Canvas.ForceUpdateCanvases();
        logScroll?.verticalNormalizedPosition.Equals(0f);
        logScroll?.velocity.Set(0, 1000); // quick snap-ish scroll
    }

    void UpdateStatus(string s) => textStatus.text = s;
}
