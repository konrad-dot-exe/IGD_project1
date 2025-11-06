using System.Threading.Tasks;
using UnityEngine;

public class MockProvider : MonoBehaviour, ILLMProvider
{
    [TextArea] public string systemPreamble = "You are a helpful assistant.";
    [Range(0f, 2f)] public float fakeLatencySeconds = 0.4f;

    public async Task<string> SendAsync(string userPrompt)
    {
        // Simulate latency
        float t = 0f;
        while (t < fakeLatencySeconds) { t += Time.deltaTime; await System.Threading.Tasks.Task.Yield(); }

        return $"Echo:\n{userPrompt}\n\n(Preamble: {systemPreamble})";
    }
}

