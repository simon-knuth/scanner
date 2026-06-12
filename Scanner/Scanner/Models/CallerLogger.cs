using System;
using System.IO;
using System.Runtime.CompilerServices;
using Serilog;

namespace Scanner.Services;

/// <summary>
///     A wrapper around Serilog's <see cref="ILogger"/> that automatically enriches every log entry with the
///     name of the source file (≈ class) that emitted it, captured at compile time via
///     <see cref="CallerFilePathAttribute"/>.
/// </summary>
/// <remarks>
///     <para>
///         The caller parameter is typed <see cref="IEquatable{String}"/> rather than <see cref="string"/> on
///         purpose. With a trailing optional <c>string</c> parameter, a log call whose last property value was a
///         <see cref="string"/> would bind that value to the caller parameter instead of the message template:
///         <c>string</c>→<c>string</c> is an identity conversion, so the lower-arity overload (which fills the
///         caller slot with the value) tied with — and then beat — the intended higher-arity overload (which had
///         to substitute the optional default). The value was silently consumed as the caller name and the final
///         <c>{Placeholder}</c> was left unrendered.
///     </para>
///     <para>
///         Typing the caller parameter as <see cref="IEquatable{String}"/> breaks that tie at compile time:
///         a real value always binds to the generic value parameter <c>T</c> via an identity conversion, which is
///         strictly better than the reference conversion required to reach an <see cref="IEquatable{String}"/>
///         parameter — so values never land in the caller slot. Anything that isn't a <see cref="string"/>
///         (including an <c>(object)</c>-typed argument) can't convert to <see cref="IEquatable{String}"/> at all,
///         so it can't be considered for that slot either. <see cref="CallerFilePathAttribute"/> still applies
///         because <c>string</c>→<see cref="IEquatable{String}"/> is a standard implicit reference conversion.
///     </para>
/// </remarks>
public class CallerLogger
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private readonly ILogger _logger;

    public ILogger InnerLogger => _logger;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public CallerLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private ILogger Enrich(IEquatable<string>? caller)
    {
        // caller is the [CallerFilePath] value (a string at runtime); strip both ".xaml" and ".cs" so e.g.
        // "...\EditorView.xaml.cs" and "...\LogService.cs" both reduce to the class name.
        string path = caller as string ?? "";
        string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
        return _logger.ForContext("Caller", name);
    }

    // VERBOSE //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Verbose(string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Verbose(messageTemplate);

    public void Verbose<T>(string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Verbose(messageTemplate, propertyValue);

    public void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Verbose(messageTemplate, propertyValue0, propertyValue1);

    public void Verbose<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Verbose(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

    public void Verbose(string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Verbose(messageTemplate, propertyValues);

    // DEBUG ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Debug(string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Debug(messageTemplate);

    public void Debug<T>(string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Debug(messageTemplate, propertyValue);

    public void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Debug(messageTemplate, propertyValue0, propertyValue1);

    public void Debug<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Debug(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

    public void Debug(string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Debug(messageTemplate, propertyValues);

    // INFORMATION //////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Information(string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Information(messageTemplate);

    public void Information<T>(string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Information(messageTemplate, propertyValue);

    public void Information<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Information(messageTemplate, propertyValue0, propertyValue1);

    public void Information<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Information(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

    public void Information(string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Information(messageTemplate, propertyValues);

    // WARNING //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Warning(string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(messageTemplate);

    public void Warning<T>(string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(messageTemplate, propertyValue);

    public void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(messageTemplate, propertyValue0, propertyValue1);

    public void Warning<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

    public void Warning(string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(messageTemplate, propertyValues);

    public void Warning(Exception exception, string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(exception, messageTemplate);

    public void Warning<T>(Exception exception, string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(exception, messageTemplate, propertyValue);

    public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(exception, messageTemplate, propertyValue0, propertyValue1);

    public void Warning(Exception exception, string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Warning(exception, messageTemplate, propertyValues);

    // ERROR ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Error(string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(messageTemplate);

    public void Error<T>(string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(messageTemplate, propertyValue);

    public void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(messageTemplate, propertyValue0, propertyValue1);

    public void Error<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

    public void Error(string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(messageTemplate, propertyValues);

    public void Error(Exception exception, string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(exception, messageTemplate);

    public void Error<T>(Exception exception, string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(exception, messageTemplate, propertyValue);

    public void Error<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(exception, messageTemplate, propertyValue0, propertyValue1);

    public void Error(Exception exception, string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Error(exception, messageTemplate, propertyValues);

    // FATAL ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Fatal(string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(messageTemplate);

    public void Fatal<T>(string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(messageTemplate, propertyValue);

    public void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(messageTemplate, propertyValue0, propertyValue1);

    public void Fatal<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

    public void Fatal(string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(messageTemplate, propertyValues);

    public void Fatal(Exception exception, string messageTemplate, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(exception, messageTemplate);

    public void Fatal<T>(Exception exception, string messageTemplate, T propertyValue, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(exception, messageTemplate, propertyValue);

    public void Fatal<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(exception, messageTemplate, propertyValue0, propertyValue1);

    public void Fatal(Exception exception, string messageTemplate, object?[] propertyValues, [CallerFilePath] IEquatable<string>? caller = null)
        => Enrich(caller).Fatal(exception, messageTemplate, propertyValues);
}
