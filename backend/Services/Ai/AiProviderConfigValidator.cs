namespace backend.Services.Ai;

public static class AiProviderConfigValidator
{
    public static (bool IsConfigured, string? MissingReason) Check(AiOptions opts) =>
        opts.Provider.Trim().ToLowerInvariant() switch
        {
            "dial" when string.IsNullOrWhiteSpace(opts.Dial.Endpoint) =>
                (false, "Ai:Dial:Endpoint is not set"),
            "dial" when string.IsNullOrWhiteSpace(opts.Dial.Model) =>
                (false, "Ai:Dial:Model is not set"),
            "dial" when string.IsNullOrWhiteSpace(opts.Dial.ApiKey) =>
                (false, "Ai:Dial:ApiKey is not set"),
            "dial" =>
                (true, null),

            "ollama" when string.IsNullOrWhiteSpace(opts.Ollama.Endpoint) =>
                (false, "Ai:Ollama:Endpoint is not set"),
            "ollama" when string.IsNullOrWhiteSpace(opts.Ollama.Model) =>
                (false, "Ai:Ollama:Model is not set"),
            "ollama" =>
                (true, null),

            _ => (true, null)
        };
}
