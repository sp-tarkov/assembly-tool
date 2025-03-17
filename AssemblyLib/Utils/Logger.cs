using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReCodeItLib.Models;

namespace ReCodeItLib.Utils;

public static class Logger
{
    public static Stopwatch Stopwatch { get; } = new();
    
    public static void DrawProgressBar(int progress, int total, int width)
    {
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, Console.CursorTop);

        var percentage = (double)progress / total;
        var completed = (int)(percentage * width);

        Console.Write("[");
        Console.Write(new string('=', completed)); // Completed part
        Console.Write(new string(' ', width - completed)); // Remaining part
        Console.Write($"] {progress}/{total} ({percentage:P0}) {Stopwatch.Elapsed.TotalSeconds:F1} seconds");
    }
    
    public static void Log(object message, ConsoleColor color = ConsoleColor.White)
    {
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

    public static void Debug(object message, ConsoleColor color = ConsoleColor.White)
    {
        
    }
}