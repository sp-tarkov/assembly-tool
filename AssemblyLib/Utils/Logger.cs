using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssemblyLib.Models;

namespace AssemblyLib.Utils;

public static class Logger
{
    public static Stopwatch Stopwatch { get; } = new();

    private static List<string> _taskExceptions = [];
    
    private static string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "log.log");

    static Logger()
    {
        var dir = Path.GetDirectoryName(_logPath);
        
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir!);
        }

        if (!File.Exists(_logPath))
        {
            File.Create(_logPath).Close();
            return;
        }

        File.Delete(_logPath);
    }
    
    public static void QueueTaskException(string exception)
    {
        _taskExceptions.Add(exception);
    }
    
    public static void Log(object message, ConsoleColor color = ConsoleColor.White, bool diskOnly = false)
    {
        using var writer = new StreamWriter(_logPath, true);
        writer.WriteLine(message);
        
        if (diskOnly) return;
        
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
        
        
    }
    
    public static void LogRemapModel(RemapModel remapModel)
    {
        JsonSerializerOptions settings = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        
        var str = JsonSerializer.Serialize(remapModel, settings);
        Log(str, ConsoleColor.Blue);
    }
}