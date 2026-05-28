using backend.Services.Ai;

namespace backend.Tests.Services.Ai;

public class AiProviderConfigValidatorTests
{
    [Fact]
    public void Dial_AllFieldsSet_IsConfigured()
    {
        var opts = new AiOptions
        {
            Provider = "dial",
            Dial = new AiOptions.DialOptions
            {
                Endpoint = "https://dial.example.com/chat/completions",
                Model    = "gpt-4o",
                ApiKey   = "sk-test"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.True(isConfigured);
        Assert.Null(reason);
    }

    [Fact]
    public void Dial_MissingApiKey_NotConfigured_ReasonMentionsKey()
    {
        var opts = new AiOptions
        {
            Provider = "dial",
            Dial = new AiOptions.DialOptions
            {
                Endpoint = "https://dial.example.com/chat/completions",
                Model    = "gpt-4o",
                ApiKey   = ""
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.False(isConfigured);
        Assert.NotNull(reason);
        Assert.Contains("Ai:Dial:ApiKey", reason);
    }

    [Fact]
    public void Dial_MissingEndpoint_NotConfigured_ReasonMentionsEndpoint()
    {
        var opts = new AiOptions
        {
            Provider = "dial",
            Dial = new AiOptions.DialOptions
            {
                Endpoint = "",
                Model    = "gpt-4o",
                ApiKey   = "sk-test"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.False(isConfigured);
        Assert.Contains("Ai:Dial:Endpoint", reason!);
    }

    [Fact]
    public void Ollama_AllFieldsSet_IsConfigured()
    {
        var opts = new AiOptions
        {
            Provider = "ollama",
            Ollama = new AiOptions.OllamaOptions
            {
                Endpoint = "http://localhost:11434",
                Model    = "llama3"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.True(isConfigured);
        Assert.Null(reason);
    }

    [Fact]
    public void Ollama_MissingEndpoint_NotConfigured_ReasonMentionsEndpoint()
    {
        var opts = new AiOptions
        {
            Provider = "ollama",
            Ollama = new AiOptions.OllamaOptions
            {
                Endpoint = "",
                Model    = "llama3"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.False(isConfigured);
        Assert.Contains("Ai:Ollama:Endpoint", reason!);
    }

    [Fact]
    public void Mock_AlwaysConfigured()
    {
        var opts = new AiOptions { Provider = "mock" };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.True(isConfigured);
        Assert.Null(reason);
    }
}
