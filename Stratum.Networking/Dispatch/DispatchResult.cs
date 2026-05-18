/*
 * (DispatchResult.cs)
 *------------------------------------------------------------
 * Created - 5/16/2026 9:50:12 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Networking.Dispatch;

/// <summary>
/// Represents the result of a dispatch operation.
/// </summary>
/// <param name="outcome">The outcome of the dispatch operation.</param>
/// <param name="typeId">The identifier of the dispatched type.</param>
/// <param name="exception">The exception that occurred during dispatch, if any.</param>
public readonly struct DispatchResult(DispatchOutcome outcome, 
    uint typeId, 
    Exception? exception = null)
{
    /// <summary>
    /// The outcome of the dispatch operation.
    /// </summary>
    public DispatchOutcome Outcome { get; } = outcome;
    /// <summary>
    /// Gets the type identifier.   
    /// </summary>
    public uint TypeId { get; } = typeId;
    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception? Exception { get; } = exception;

    /// <summary>
    /// Gets a value indicating whether the dispatch outcome is Ok.
    /// </summary>
    public bool IsOk => Outcome == DispatchOutcome.Ok;

    /// <summary>
    /// Creates a successful dispatch result with the specified type identifier.
    /// </summary>
    /// <param name="typeId">The type identifier for the dispatch result.</param>
    /// <returns>A new <see cref="DispatchResult"/> instance with <see cref="DispatchOutcome.Ok"/> outcome.</returns>
    public static DispatchResult Ok(uint typeId) => new(DispatchOutcome.Ok, typeId);
    /// <summary>
    /// Creates a dispatch result indicating an unknown type.
    /// </summary>
    /// <param name="typeId">The identifier of the unknown type.</param>
    /// <returns>A dispatch result with an unknown type outcome.</returns>
    public static DispatchResult Unknown(uint typeId) => new(DispatchOutcome.UnknownType, typeId);
    /// <summary>
    /// Creates a dispatch result indicating an invalid packet.
    /// </summary>
    /// <param name="typeId">The type identifier of the packet.</param>
    /// <param name="ex">The exception that occurred during packet processing.</param>
    /// <returns>A dispatch result with an <see cref="DispatchOutcome.InvalidPacket"/> outcome.</returns>
    public static DispatchResult Invalid(uint typeId, Exception ex) => new(DispatchOutcome.InvalidPacket, typeId, ex);
    /// <summary>
    /// Creates a dispatch result indicating a handler exception occurred.
    /// </summary>
    /// <param name="typeId">The type identifier of the handler that threw the exception.</param>
    /// <param name="ex">The exception thrown by the handler.</param>
    /// <returns>A new dispatch result with the handler exception outcome.</returns>
    public static DispatchResult Handler(uint typeId, Exception ex) => new(DispatchOutcome.HandlerException, typeId, ex);
}

/*
*------------------------------------------------------------
* (DispatchResult.cs)
* See License.txt for licensing information.
*-----------------------------------------------------------
*/