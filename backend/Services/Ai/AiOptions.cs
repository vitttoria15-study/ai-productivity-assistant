namespace backend.Services.Ai;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "mock";

    public DialOptions   Dial   { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();

    public class DialOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Model    { get; set; } = "gpt-4o";

        /// <summary>
        /// Populated from .NET user-secrets or the Ai__Dial__ApiKey environment variable.
        /// Never set in appsettings.json or appsettings.Development.json.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;  // populated from user-secrets or Ai__Dial__ApiKey env var; never from appsettings
    }

    public class OllamaOptions
    {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string Model    { get; set; } = "llama3";
    }
}
