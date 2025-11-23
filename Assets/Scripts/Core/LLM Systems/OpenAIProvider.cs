using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;                 // <-- from the package we installed
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIProvider : MonoBehaviour, ILLMProvider
{
    // [Header("OpenAI")]
    // [Tooltip("Paste your API key here in the Editor for now (don’t commit to git).")]
    // public string apiKey;

    [Tooltip("Chat Completions endpoint")]
    public string endpoint = "https://api.openai.com/v1/chat/completions";

    [Tooltip("Good fast model for prototyping.")]
    public string model = "gpt-4o-mini";

    [TextArea] public string systemPreamble =
        "You are a helpful assistant inside a Unity music education prototype.";

    public async Task<string> SendAsync(string userPrompt)
    {
        var apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        //Debug.Log(apiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new System.Exception("OpenAIProvider: Missing OPENAI_API_KEY (env).");

        // Optional: support org via env
        var orgId = System.Environment.GetEnvironmentVariable("OPENAI_ORG_ID");

        var reqBody = new ChatRequest
        {
            model = model,
            messages = new List<ChatMessage>
            {
                new ChatMessage { role="system", content=systemPreamble },
                new ChatMessage { role="user",   content=userPrompt }
            }
        };

        string json = JsonConvert.SerializeObject(reqBody);

        using (var req = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            if (!string.IsNullOrEmpty(orgId))
                req.SetRequestHeader("OpenAI-Organization", orgId);
            req.SetRequestHeader("Accept", "application/json");

            // Prevent indefinite hang
            req.timeout = 60; // seconds

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            // Better error surfacing
            if (req.result != UnityWebRequest.Result.Success)
                throw new System.Exception(
                    $"HTTP {(int)req.responseCode} {req.result}: {req.error}\n{req.downloadHandler.text}");

            var responseJson = req.downloadHandler.text;
            var parsed = JsonConvert.DeserializeObject<ChatResponse>(responseJson);

            if (parsed?.choices != null && parsed.choices.Count > 0)
                return parsed.choices[0]?.message?.content ?? "(Empty content)";

            return "(No content in response.)";
        }
    }

    // // ---- DTOs ----
    // [System.Serializable]
    // public class ChatRequest
    // {
    //     public string model;
    //     public List<Message> messages;

    //     // Optional tuning:
    //     public float? temperature;
    //     public int? max_tokens;

    //     // Strict JSON responses when needed:
    //     public ResponseFormat response_format;
    // }

    // [System.Serializable]
    // public class ResponseFormat { public string type; } // e.g., "json_object"
}

// ===== DTOs (outside the class) =====
[System.Serializable]
public class ChatRequest
{
    public string model;
    public List<ChatMessage> messages;
    public float? temperature;
    public int? max_tokens;
    public ResponseFormat response_format;
}

[System.Serializable] public class ChatMessage { public string role; public string content; }
[System.Serializable] public class ChatResponse { public List<Choice> choices; }
[System.Serializable] public class Choice { public ChatMessage message; public int index; public object logprobs; public string finish_reason; }
[System.Serializable] public class ResponseFormat { public string type; }
