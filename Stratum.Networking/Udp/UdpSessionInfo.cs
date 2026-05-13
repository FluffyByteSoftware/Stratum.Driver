/*
 * (UdpSessionInfo.cs)
 *------------------------------------------------------------
 * Created - 5/12/2026 3:57:19 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Networking.Udp;

public readonly record struct UdpSessionInfo(
    Guid SessionId,
    string Username,
    DateTime IssuedUtc,
    DateTime ExpiresUtc);


/*
 *------------------------------------------------------------
 * (UdpSessionInfo.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */