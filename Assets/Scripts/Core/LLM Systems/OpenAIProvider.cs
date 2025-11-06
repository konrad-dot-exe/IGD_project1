using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;                 // <-- from the package we installed
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIProvider : MonoBehaviour, ILLMProvider
{
    [Header("OpenAI")]
    [Tooltip("Paste your API key here in the Editor for now (don’t commit to git).")]
    public string apiKey;

    [Tooltip("Chat Completions endpoint")]
    public string endpoint = "https://api.openai.com/v1/chat/completions";

    [Tooltip("Good fast model for prototyping.")]
    public string model = "gpt-4o-mini";

    [TextArea] public string systemPreamble =
        "You are a helpful assistant inside a Unity music education prototype.";

    public async Task<string> SendAsync(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new System.Exception("OpenAIProvider: Missing API key.");

        var reqBody = new ChatRequest
        {
            model = model,
            messages = new List<Message>
            {
                new Message{ role="system", content=systemPreamble },
                new Message{ role="user", content=userPrompt }
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

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new System.Exception($"HTTP error: {req.result} | {req.error} | {req.downloadHandler.text}");

            var responseJson = req.downloadHandler.text;
            var parsed = JsonConvert.DeserializeObject<ChatResponse>(responseJson);

            if (parsed?.choices != null && parsed.choices.Count > 0)
                return parsed.choices[0]?.message?.content?.Replace("\\n", "\n");

            return "(No content in response.)";
        }
    }

    // ====== DTOs for request/response ======
    [System.Serializable]
    public class ChatRequest
    {
        public string model;
        public List<Message> messages;
        // You can add temperature, max_tokens, response_format, etc.
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ChatResponse
    {
        public List<Choice> choices;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
        public int index;
        public object logprobs; // unused
        public string finish_reason;
    }
}

