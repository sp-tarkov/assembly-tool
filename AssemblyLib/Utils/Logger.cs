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
    
    public static async Task DrawProgressBar(List<Task> tasks, string stageText)
    {
        var totalTasks = tasks.Count;
        var completedTasks = 0;
        
        var initialTop = Console.CursorTop; // Store initial cursor position
        await foreach (var taskResult in tasks.ToAsyncEnumerable())
        {
            await taskResult;
            completedTasks++;
            UpdateProgressBar(completedTasks, totalTasks, initialTop + 1, stageText);
        }

        if (_taskExceptions.Count == 0) return;
        
        foreach (var ex in _taskExceptions)
        {
            Log(ex);
        }
        
        _taskExceptions.Clear();
    }

    public static void QueueTaskException(string exception)
    {
        _taskExceptions.Add(exception);
    }
    
    private static void UpdateProgressBar(int progress, int total, int progressBarLine, string stageText)
    {
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, progressBarLine); //set the line to draw the bar on.
        
        const int width = 50;
        const int stageTextWidth = 30; // Adjust as needed
        const int timeWidth = 20; // Adjust as needed
        
        var percentage = (double)progress / total;
        var completed = (int)(percentage * width);
        
        var paddedStageText = $"{stageText,-stageTextWidth}";
        var paddedProgress = $"{progress}/{total} ({percentage:P0})".PadRight(timeWidth);
        var paddedTime = $"{Stopwatch.Elapsed.TotalSeconds:F1} seconds";
        
        Console.Write($"{paddedStageText} [");
        Console.Write(new string('=', completed)); // Completed part
        Console.Write(new string(' ', width - completed)); // Remaining part
        Console.Write($"] {paddedProgress} {paddedTime}");
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