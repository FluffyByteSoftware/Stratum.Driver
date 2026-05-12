/*
 * (DispatchOutcome.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 10:15:13 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Networking.Dispatch;

/// <summary>
/// Specifies the possible outcomes of a packet dispatch operation.
/// </summary>
/// <remarks>Use this enumeration to determine the result of dispatching a packet, such as whether it was handled
/// successfully, the packet type was unknown, the packet was invalid, or an exception occurred in the
/// handler.</remarks>
public enum DispatchOutcome : byte
{
    /// <summary>
    /// Indicates that the operation or event was handled successfully and no further action is required.
    /// </summary>
    Handled = 0,
    /// <summary>
    /// Represents an unknown or unspecified type.
    /// </summary>
    UnknownType,
    /// <summary>
    /// Represents an error condition indicating that a packet is invalid or malformed.
    /// </summary>
    /// <remarks>Use this value to identify or handle cases where a packet does not conform to the expected
    /// format or protocol requirements.</remarks>
    InvalidPacket,
    /// <summary>
    /// Represents an exception that is thrown by a handler during the processing of a request or operation.
    /// </summary>
    /// <remarks>Use this exception to indicate errors that occur within handler logic, allowing for more
    /// precise error handling and reporting in handler-based architectures.</remarks>
    HandlerException
}



/*
 *------------------------------------------------------------
 * (DispatchOutcome.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */