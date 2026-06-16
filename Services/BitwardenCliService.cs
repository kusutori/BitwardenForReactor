using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BitwardenForReactor.Models;

namespace BitwardenForReactor.Services;

[JsonSerializable(typeof(BitwardenStatus))]
[JsonSerializable(typeof(BitwardenItem[]))]
[JsonSerializable(typeof(BitwardenFolder[]))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class BitwardenJsonContext : JsonSerializerContext
{
}

public sealed partial class BitwardenCliService
{
    public static BitwardenCliService Instance { get; } = new();

    public string? SessionKey { get; private set; }

    public bool IsUnlocked => !string.IsNullOrEmpty(SessionKey);

    private BitwardenCliService()
    {
    }

    public static async Task<BitwardenStatus?> GetStatusAsync()
    {
        var result = await ExecuteCommandAsync("status");
        if (!result.Success)
        {
            Debug.WriteLine($"bw status failed: {result.Error}");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(result.Output, BitwardenJsonContext.Default.BitwardenStatus);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to parse status: {ex.Message}");
            return null;
        }
    }

    public async Task<(bool Success, string Message)> UnlockAsync(string masterPassword)
    {
        var result = await ExecuteCommandAsync("unlock", masterPassword, "--raw");
        string? sessionKey = null;

        if (!string.IsNullOrWhiteSpace(result.Output) &&
            !result.Output.Contains("Your vault is now unlocked", StringComparison.OrdinalIgnoreCase))
        {
            sessionKey = result.Output.Trim();
        }

        if (sessionKey is null)
        {
            var sessionMatch = Regex.Match(result.Output, @"BW_SESSION=""([^""]+)""");
            if (sessionMatch.Success)
            {
                sessionKey = sessionMatch.Groups[1].Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            SessionKey = sessionKey;
            if (!result.Success && IsNetworkError(result.Error))
            {
                return (true, "密码库已解锁，但网络不可用，将使用本地数据。");
            }

            return (true, "密码库已解锁。");
        }

        if (IsInvalidPasswordError(result.Error))
        {
            return (false, "主密码不正确。");
        }

        return (false, string.IsNullOrWhiteSpace(result.Error) ? "解锁失败。" : result.Error);
    }

    public async Task<bool> LockAsync()
    {
        var result = await ExecuteCommandAsync("lock");
        if (result.Success)
        {
            SessionKey = null;
        }

        return result.Success;
    }

    public async Task<bool> SyncAsync()
    {
        var result = await ExecuteCommandAsync(WithSession("sync"));
        return result.Success;
    }

    public async Task<BitwardenItem[]?> GetItemsAsync()
    {
        var result = await ExecuteCommandAsync(WithSession("list", "items"));
        return DeserializeArray<BitwardenItem>(result, BitwardenJsonContext.Default.BitwardenItemArray);
    }

    public async Task<BitwardenItem[]?> GetTrashItemsAsync()
    {
        var result = await ExecuteCommandAsync(WithSession("list", "items", "--trash"));
        return DeserializeArray<BitwardenItem>(result, BitwardenJsonContext.Default.BitwardenItemArray);
    }

    public async Task<BitwardenFolder[]?> GetFoldersAsync()
    {
        var result = await ExecuteCommandAsync(WithSession("list", "folders"));
        return DeserializeArray<BitwardenFolder>(result, BitwardenJsonContext.Default.BitwardenFolderArray);
    }

    public async Task<string?> GetTotpAsync(string itemId)
    {
        var result = await ExecuteCommandAsync(WithSession("get", "totp", itemId));
        return result.Success && !string.IsNullOrWhiteSpace(result.Output) ? result.Output.Trim() : null;
    }

    public async Task<bool> DeleteItemAsync(string itemId, bool permanent = false)
    {
        var result = permanent
            ? await ExecuteCommandAsync(WithSession("delete", "item", itemId, "--permanent"))
            : await ExecuteCommandAsync(WithSession("delete", "item", itemId));
        return result.Success;
    }

    public async Task<bool> RestoreItemAsync(string itemId)
    {
        var result = await ExecuteCommandAsync(WithSession("restore", "item", itemId));
        return result.Success;
    }

    public async Task<bool> CreateItemAsync(JsonObject newItem)
    {
        var encodedJson = EncodeJson(newItem);
        var result = await ExecuteCommandAsync(WithSession("create", "item", encodedJson));
        return result.Success;
    }

    public async Task<bool> EditItemAsync(string itemId, JsonObject updatedFields)
    {
        var getResult = await ExecuteCommandAsync(WithSession("get", "item", itemId));
        if (!getResult.Success)
        {
            return false;
        }

        var currentItem = JsonNode.Parse(getResult.Output)?.AsObject();
        if (currentItem is null)
        {
            return false;
        }

        foreach (var field in updatedFields)
        {
            currentItem[field.Key] = field.Value?.DeepClone();
        }

        var encodedJson = EncodeJson(currentItem);
        var editResult = await ExecuteCommandAsync(WithSession("edit", "item", itemId, encodedJson));
        return editResult.Success;
    }

    public async Task<string?> GeneratePasswordAsync(
        int length = 14,
        bool uppercase = true,
        bool lowercase = true,
        bool number = true,
        bool special = false)
    {
        var args = new System.Collections.Generic.List<string> { "generate" };
        if (uppercase) args.Add("-u");
        if (lowercase) args.Add("-l");
        if (number) args.Add("-n");
        if (special) args.Add("-s");
        args.Add("--length");
        args.Add(Math.Max(5, length).ToString(System.Globalization.CultureInfo.InvariantCulture));

        var result = await ExecuteCommandAsync(args.ToArray());
        return result.Success ? result.Output.Trim() : null;
    }

    public void ClearSession() => SessionKey = null;

    private static T[]? DeserializeArray<T>(CommandResult result, JsonTypeInfo<T[]> jsonTypeInfo)
    {
        if (!result.Success)
        {
            Debug.WriteLine($"bw command failed: {result.Error}");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(result.Output, jsonTypeInfo);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to parse bw output: {ex.Message}");
            return null;
        }
    }

    private static string EncodeJson(JsonObject obj) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(obj.ToJsonString()));

    private string[] WithSession(params string[] arguments)
    {
        if (string.IsNullOrWhiteSpace(SessionKey))
        {
            return arguments;
        }

        return [.. arguments, "--session", SessionKey];
    }

    private static async Task<CommandResult> ExecuteCommandAsync(params string[] arguments)
    {
        var settings = SettingsManager.Instance.Current;
        var processInfo = new ProcessStartInfo
        {
            FileName = settings.BwPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            processInfo.ArgumentList.Add(argument);
        }

        foreach (var kvp in settings.GetEnvironmentVariables())
        {
            processInfo.Environment[kvp.Key] = kvp.Value;
        }

        try
        {
            using var process = new Process { StartInfo = processInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            return new CommandResult(
                outputBuilder.ToString().Trim(),
                errorBuilder.ToString().Trim(),
                process.ExitCode);
        }
        catch (Exception ex)
        {
            return new CommandResult(string.Empty, ex.Message, -1);
        }
    }

    private static bool IsNetworkError(string error)
    {
        var lowerError = error.ToLowerInvariant();
        return lowerError.Contains("network") ||
               lowerError.Contains("socket") ||
               lowerError.Contains("tls") ||
               lowerError.Contains("ssl") ||
               lowerError.Contains("econnreset") ||
               lowerError.Contains("econnrefused") ||
               lowerError.Contains("timeout") ||
               lowerError.Contains("fetch") ||
               lowerError.Contains("api.bitwarden.com") ||
               lowerError.Contains("identity.bitwarden.com");
    }

    private static bool IsInvalidPasswordError(string error) =>
        error.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("wrong", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("incorrect", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("password", StringComparison.OrdinalIgnoreCase);

    private sealed record CommandResult(string Output, string Error, int ExitCode)
    {
        public bool Success => ExitCode == 0;
    }
}
