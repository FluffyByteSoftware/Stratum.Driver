/*
 * (DispatchResult.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 10:15:52 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Networking.Dispatch;

/// <summary>
/// Represents the result of a dispatch operation, including its outcome, associated type identifier, and any exception
/// that occurred.
/// </summary>
/// <param name="outcome">The outcome of the dispatch operation.</param>
/// <param name="typeId">The unique identifier for the type associated with this dispatch result.</param>
/// <param name="exception">The exception that caused the dispatch operation to fail, or null if the operation was successful.</param>
public readonly struct DispatchResult(DispatchOutcome outcome, uint typeId, Exception? exception = null)
{
    /// <summary>
    /// Gets the result of the dispatch operation.
    /// </summary>
    public DispatchOutcome Outcome { get; } = outcome;
    /// <summary>
    /// Gets the unique identifier for the type represented by this instance.
    /// </summary>
    public uint TypeId { get; } = typeId;
    /// <summary>
    /// Gets the exception that caused the current operation to fail, if any.
    /// </summary>
    public Exception? Exception { get; } = exception;
    /// <summary>
    /// Gets a value indicating whether the dispatch operation resulted in an error.
    /// </summary>
    public bool IsError => Outcome != DispatchOutcome.Handled;

    /// <summary>
    /// Creates a new DispatchResult indicating that the packet was successfully handled.
    /// </summary>
    /// <param name="typeId">The type identifier of the packet that was handled.</param>
    /// <returns>A DispatchResult representing a successfully handled packet.</returns>
    public static DispatchResult Handled(uint typeId) => new(DispatchOutcome.Handled, typeId);
}



/*
 *------------------------------------------------------------
 * (DispatchResult.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */