using System;

namespace Scanner.Extensions;

public static class ExceptionExtensions
{
    /// <summary>
    ///     Gets the restricted description that WinRT attached to the exception (or one of its inner exceptions), if any.
    ///     It is set by the failing component (e.g. the scanner driver) and may tell the user how to resolve the error.
    /// </summary>
    public static string? GetRestrictedDescription(this Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Data["RestrictedDescription"] is string restrictedDescription && !string.IsNullOrWhiteSpace(restrictedDescription))
                return restrictedDescription.Trim();
        }

        return null;
    }
}
