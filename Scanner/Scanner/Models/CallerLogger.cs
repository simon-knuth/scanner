using System;
using System.IO;
using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Events;

namespace Scanner.Services
{
    /// <summary>
    ///     A wrapper around Serilog's <see cref="ILogger"/> that automatically enriches
    ///     every log entry with the calling member name, source file, and line number.
    /// </summary>
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
        private ILogger Enrich(string memberName)
        {
            return _logger
                .ForContext("Caller", memberName);
        }

        public void Verbose(
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Verbose(messageTemplate);
        }

        public void Verbose<T>(
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Verbose(messageTemplate, propertyValue);
        }

        public void Verbose<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Verbose(messageTemplate, propertyValue0, propertyValue1);
        }

        public void Verbose<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Verbose(messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Verbose(
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Verbose(messageTemplate, propertyValues);
        }

        public void Debug(
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Debug(messageTemplate);
        }

        public void Debug<T>(
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Debug(messageTemplate, propertyValue);
        }

        public void Debug<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Debug(messageTemplate, propertyValue0, propertyValue1);
        }

        public void Debug<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Debug(messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Debug(
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Debug(messageTemplate, propertyValues);
        }

        public void Information(
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Information(messageTemplate);
        }

        public void Information<T>(
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Information(messageTemplate, propertyValue);
        }

        public void Information<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Information(messageTemplate, propertyValue0, propertyValue1);
        }

        public void Information<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Information(messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Information(
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Information(messageTemplate, propertyValues);
        }

        public void Warning(
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(messageTemplate);
        }

        public void Warning<T>(
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(messageTemplate, propertyValue);
        }

        public void Warning<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(messageTemplate, propertyValue0, propertyValue1);
        }

        public void Warning<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Warning(
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(messageTemplate, propertyValues);
        }

        public void Warning(
            Exception exception,
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(exception, messageTemplate);
        }

        public void Warning<T>(
            Exception exception,
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(exception, messageTemplate, propertyValue);
        }

        public void Warning(
            Exception exception,
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Warning(exception, messageTemplate, propertyValues);
        }

        public void Error(
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(messageTemplate);
        }

        public void Error<T>(
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(messageTemplate, propertyValue);
        }

        public void Error<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(messageTemplate, propertyValue0, propertyValue1);
        }

        public void Error<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Error(
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(messageTemplate, propertyValues);
        }

        public void Error(
            Exception exception,
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(exception, messageTemplate);
        }

        public void Error<T>(
            Exception exception,
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(exception, messageTemplate, propertyValue);
        }

        public void Error<T0, T1>(
            Exception exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(exception, messageTemplate, propertyValue0, propertyValue1);
        }

        public void Error(
            Exception exception,
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Error(exception, messageTemplate, propertyValues);
        }

        public void Fatal(
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(messageTemplate);
        }

        public void Fatal<T>(
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(messageTemplate, propertyValue);
        }

        public void Fatal<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(messageTemplate, propertyValue0, propertyValue1);
        }

        public void Fatal<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Fatal(
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(messageTemplate, propertyValues);
        }

        public void Fatal(
            Exception exception,
            string messageTemplate,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(exception, messageTemplate);
        }

        public void Fatal<T>(
            Exception exception,
            string messageTemplate,
            T propertyValue,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(exception, messageTemplate, propertyValue);
        }

        public void Fatal(
            Exception exception,
            string messageTemplate,
            object[] propertyValues,
            [CallerFilePath] string memberName = "")
        {
            Enrich(Path.GetFileNameWithoutExtension(memberName))
                .Fatal(exception, messageTemplate, propertyValues);
        }
    }
}