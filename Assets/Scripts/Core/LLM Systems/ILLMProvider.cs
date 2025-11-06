using System.Threading.Tasks;

public interface ILLMProvider
{
    Task<string> SendAsync(string userPrompt);
}

