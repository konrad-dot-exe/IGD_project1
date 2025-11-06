using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    public TMP_InputField inputPrompt;
    public TMP_Text textResponse;
    public TMP_Text textStatus;   // optional
    public Button buttonSend;
    public LLMManager llm;

    void Start()
    {
        if (!ValidateRefs()) return;
        buttonSend.onClick.AddListener(OnSendClicked);
        SetStatus("Idle");
    }

    bool ValidateRefs()
    {
        if (inputPrompt == null) { Debug.LogError("ChatUI: Input Prompt not assigned."); return false; }
        if (textResponse == null) { Debug.LogError("ChatUI: Text Response not assigned."); return false; }
        if (buttonSend == null) { Debug.LogError("ChatUI: Button Send not assigned."); return false; }
        if (llm == null) { Debug.LogError("ChatUI: LLMManager not assigned."); return false; }
        return true;
    }

    void SetStatus(string s)
    {
        if (textStatus != null) textStatus.text = s;
    }

    async void OnSendClicked()
    {
        var prompt = inputPrompt.text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            SetStatus("Enter a prompt.");
            return;
        }

        buttonSend.interactable = false;
        SetStatus("Thinking…");

        string reply;
        try
        {
            reply = await llm.SendPromptAsync(prompt);
        }
        catch (System.Exception ex)
        {
            reply = "Error: " + ex.Message;
            Debug.LogError(ex);
        }

        textResponse.text = reply;
        SetStatus("Done");
        buttonSend.interactable = true;
    }
}
