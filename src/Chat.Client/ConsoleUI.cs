namespace Chat.Client;

/// <summary>
/// Handles console rendering for the chat client.
/// </summary>
public static class ConsoleUI
{
    private static readonly object WriteLock = new();

    public static void WriteHeader()
    {
        Console.WriteLine("=== MagicOnion Chat Client ===");
        Console.WriteLine();
    }

    public static void WriteSeparator(int length = 60)
    {
        Console.WriteLine(new string('-', length));
    }

    public static string? PromptForInput(string prompt, string? defaultValue = null)
    {
        if (defaultValue is not null)
        {
            Console.Write($"{prompt} (press Enter for {defaultValue}): ");
        }
        else
        {
            Console.Write($"{prompt}: ");
        }

        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    public static string PromptForRequiredInput(string prompt)
    {
        string? input;
        do
        {
            Console.Write($"{prompt}: ");
            input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                WriteWarning($"{prompt.TrimEnd(':')} cannot be empty!");
            }
        } while (string.IsNullOrWhiteSpace(input));

        return input;
    }

    public static void WriteInfo(string message)
    {
        lock (WriteLock)
        {
            Console.WriteLine(message);
        }
    }

    public static void WriteSuccess(string message)
    {
        lock (WriteLock)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ForegroundColor = color;
        }
    }

    public static void WriteWarning(string message)
    {
        lock (WriteLock)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ForegroundColor = color;
        }
    }

    public static void WriteError(string message)
    {
        lock (WriteLock)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {message}");
            Console.ForegroundColor = color;
        }
    }

    public static void WriteSystemMessage(string timestamp, string username, string message)
    {
        lock (WriteLock)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{timestamp}] [{username}]: {message}");
            Console.ForegroundColor = color;
        }
    }

    public static void WriteUserMessage(string timestamp, string username, string message)
    {
        lock (WriteLock)
        {
            Console.WriteLine($"[{timestamp}] {username}: {message}");
        }
    }

    public static void WriteConnectionState(ConnectionState state)
    {
        lock (WriteLock)
        {
            var (color, text) = state switch
            {
                ConnectionState.Connecting => (ConsoleColor.Cyan, "Connecting..."),
                ConnectionState.Connected => (ConsoleColor.Green, "Connected"),
                ConnectionState.Reconnecting => (ConsoleColor.Yellow, "Reconnecting..."),
                ConnectionState.Disconnected => (ConsoleColor.Red, "Disconnected"),
                _ => (ConsoleColor.White, state.ToString())
            };

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[Status] {text}");
            Console.ForegroundColor = originalColor;
        }
    }
}