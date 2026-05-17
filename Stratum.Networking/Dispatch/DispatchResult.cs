/*
 * (DispatchResult.cs)
 *------------------------------------------------------------
 * Created - 5/16/2026 9:50:12 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Networking.Dispatch
{
    public readonly struct DispatchResult(DispatchOutcome outcome, 
        uint typeId, 
        Exception? exception = null)
    {
        public DispatchOutcome Outcome { get; } = outcome;
        public uint TypeId { get; } = typeId;
        public Exception? Exception { get; } = exception;

        public bool IsOk => Outcome == DispatchOutcome.Ok;

    }
}

/*
 *------------------------------------------------------------
 * (DispatchResult.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */