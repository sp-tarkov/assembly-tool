using System.Collections.Concurrent;
using Newtonsoft.Json;
using ReCodeItLib.Models;

namespace ReCodeItLib.Utils;

public static class Logger
{
    // This queue will hold the messages to then place them on the wait list
    private static readonly ConcurrentQueue<LogMessage> _messages = new();
    private static bool Running = true;
    private static bool IsTerminated;
    // This dictionary acts as a waitlist, we are going to wait _defaultWaitTimeMs before logging all the messages
    // coming from certain thread into the console, this way we can make sure they are grouped in relevance
    private static readonly Dictionary<int, HeldMessages> _heldMessages = new();
    // This is the timeout we will wait before logging a whole group of messages coming from a single thread
    private static readonly TimeSpan _defaultWaitTimeMs = TimeSpan.FromMilliseconds(500);
    
    static Logger()
    {
        if (File.Exists(_logPath))
        {
            File.Delete(_logPath);
            File.Create(_logPath).Close();
        }

        Task.Factory.StartNew(LogThread, TaskCreationOptions.LongRunning);
    }

    private static void LogThread()
    {
        while (Running || _heldMessages.Count > 0)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            // Check the message queue and add them to the waitlist
            CheckAndHoldMessages();
            // Check the waitlist messages and see if any are ready to be logged
            LogHeldMessages();
        }

        IsTerminated = true;
    }

    private static void LogHeldMessages()
    {
        var currentLogExecution = DateTime.Now;
        foreach (var heldMessagesKP in _heldMessages)
        {
            var heldMessages = heldMessagesKP.Value;
            if (currentLogExecution - heldMessages.FirstInsertion > _defaultWaitTimeMs)
            {
                while (heldMessages.Messages.TryDequeue(out var messageToLog))
                    LogInternal(messageToLog);
                _heldMessages.Remove(heldMessagesKP.Key);
            }
        }
    }

    private static void CheckAndHoldMessages()
    {
        var currentLogExecution = DateTime.Now;
        while (_messages.TryDequeue(out var messageToHold))
        {
            if (!_heldMessages.TryGetValue(messageToHold.ThreadId, out var heldMessages))
            {
                heldMessages = new HeldMessages
                {
                    FirstInsertion = currentLogExecution,
                    ThreadID = messageToHold.ThreadId
                };
                _heldMessages.Add(heldMessages.ThreadID, heldMessages);
            }
            heldMessages.Messages.Enqueue(messageToHold);
        }
    }

    public static void Terminate()
    {
        Running = false;
    }

    public static bool IsRunning()
    {
        return !IsTerminated;
    }

    public static void DrawProgressBar(int progress, int total, int width)
    {
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, Console.CursorTop);

        var percentage = (double)progress / total;
        var completed = (int)(percentage * width);

        Console.Write("[");
        Console.Write(new string('=', completed)); // Completed part
        Console.Write(new string(' ', width - completed)); // Remaining part
        Console.Write($"] {progress}/{total} ({percentage:P0})");
    }
    
    private const string _defaultFileName = "ReCodeIt.log";
    private static string _logPath => Path.Combine(AppContext.BaseDirectory, "Data", "ReCodeIt.log");
    public static void ClearLog()
    {
        if (File.Exists(_logPath))
        {
            File.Delete(_logPath);
            File.Create(_logPath).Close();
        }
    }

    public static void Log(object message, ConsoleColor color = ConsoleColor.Gray, bool silent = false)
    {
        _messages.Enqueue(new LogMessage {Message = message, Color = color, Silent = silent, ThreadId = Thread.CurrentThread.ManagedThreadId});
    }

    public static void LogSync(object message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
    
    public static void LogRemapModel(RemapModel remapModel)
    {
        var settings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        
        var str = JsonConvert.SerializeObject(remapModel, Formatting.Indented, settings);
        LogSync(str, ConsoleColor.Blue);
    }
    
    private static void LogInternal(LogMessage message)
    {
        if (!message.Silent)
        {
            Console.ForegroundColor = message.Color;
            Console.WriteLine(message.Message);
            Console.ResetColor();
        }

        //WriteToDisk(message.Message);
    }

    private static void WriteToDisk(object message)
    {
        try
        {
            using (StreamWriter sw = File.AppendText(_logPath))
            {
                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
                sw.Close();
            }
        }
        catch (IOException ex)
        {
            // Handle potential file writing errors gracefully
            Console.WriteLine($"Error logging: {ex.Message}");
        }
    }
    private class LogMessage
    {
        public object? Message { get; init; }
        public ConsoleColor Color { get; init; }
        public bool Silent { get; init; }
        public int ThreadId { get; init; }
    }

    private class HeldMessages
    {
        public int ThreadID { get; init; }
        public DateTime FirstInsertion { get; init; }
        public Queue<LogMessage> Messages { get; } = new(10);
    }
}