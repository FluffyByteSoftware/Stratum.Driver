/*
 * (DispatchOutcome.cs)
 *------------------------------------------------------------
 * Created - 5/16/2026 9:49:36 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Networking.Dispatch;


/// <summary>
/// Represents the result of a dispatch operation.
/// </summary>
public enum DispatchOutcome
{
    /// <summary>
    /// Represents a successful result.
    /// </summary>
    Ok,
    /// <summary>
    /// Represents an unknown type.
    /// </summary>
    UnknownType,
    /// <summary>
    /// Invalid packet.
    /// </summary>
    InvalidPacket,
    /// <summary>
    /// Represents errors that occur during handler execution.
    /// </summary>
    HandlerException
}


/*
*------------------------------------------------------------
* (DispatchOutcome.cs)
* See License.txt for licensing information.
*-----------------------------------------------------------
*/