using System.Threading.Tasks;
using UnityEngine;

public class LLMManager : MonoBehaviour
{
    public static int TotalApiCalls { get; private set; } = 0;
    public static event System.Action<int> OnApiCountChanged;

    [Header("Provider (drag a MockProvider or OpenAIProvider here)")]
    public MonoBehaviour providerComponent; // must implement ILLMProvider

    ILLMProvider _provider;

    void Awake()
    {
        _provider = providerComponent as ILLMProvider;
        if (_provider == null)
            Debug.LogError("Provider component does not implement ILLMProvider.");
    }

    public async Task<string> SendPromptAsync(string prompt)
    {
        var result = await _provider.SendAsync(prompt);

        TotalApiCalls++;
        OnApiCountChanged?.Invoke(TotalApiCalls);

        Debug.Log($"[LLM] Total API calls so far: {TotalApiCalls}");

        return result;
    }
}
