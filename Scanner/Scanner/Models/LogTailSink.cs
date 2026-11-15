using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Scanner.Models;

/// <summary>
///     A Serilog sink that keeps only the latest log entries in memory and writes them to a target logger on demand,
///     instead of passing on every entry as it comes in.
/// </summary>
internal sealed class LogTailSink : ILogEventSink
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private readonly ILogger target;
    private readonly int capacity;
    private readonly Queue<LogEvent> entries = new();
    private readonly Lock entriesLock = new();


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <param name="target">
    ///     The logger that <see cref="Send"/> writes the kept entries to. Not disposed with this sink, which lives on
    ///     after the logger that feeds it is closed, so entries can still be sent while a crash is reported.
    /// </param>
    /// <param name="capacity">How many of the latest entries to keep.</param>
    public LogTailSink(ILogger target, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        this.target = target;
        this.capacity = capacity;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Emit(LogEvent logEvent)
    {
        lock (entriesLock)
        {
            entries.Enqueue(logEvent);
            if (entries.Count > capacity)
            {
                entries.Dequeue();
            }
        }
    }

    /// <summary>
    ///     Writes the kept entries to the target logger and forgets them, so the next call only sends newer entries.
    /// </summary>
    public void Send()
    {
        LogEvent[] pending;
        lock (entriesLock)
        {
            pending = entries.ToArray();
            entries.Clear();
        }

        foreach (LogEvent logEvent in pending)
        {
            target.Write(logEvent);
        }
    }
}
