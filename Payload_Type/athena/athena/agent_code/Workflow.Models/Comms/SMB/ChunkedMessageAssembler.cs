using System.Collections.Concurrent;
using System.Text;

namespace Workflow.Models
{
    public class ChunkedMessageAssembler
    {
        private readonly ConcurrentDictionary<string, StringBuilder>
            _partialMessages = new();
        private readonly ConcurrentDictionary<string, int>
            _expectedSequence = new();
        private readonly object _lock = new();

        public enum Result
        {
            Accepted,
            Complete,
            OutOfOrder,
        }

        public Result AddChunk(
            string guid,
            int sequence,
            string content,
            bool isFinal,
            out string? fullMessage)
        {
            fullMessage = null;

            lock (_lock)
            {
                _partialMessages.TryAdd(guid, new StringBuilder());
                int expected =
                    _expectedSequence.GetOrAdd(guid, 0);

                if (sequence != expected)
                {
                    _partialMessages.TryRemove(guid, out _);
                    _expectedSequence.TryRemove(guid, out _);
                    return Result.OutOfOrder;
                }

                _expectedSequence[guid] = expected + 1;
                _partialMessages[guid].Append(content);

                if (!isFinal)
                {
                    return Result.Accepted;
                }

                if (_partialMessages.TryRemove(
                        guid, out var sb))
                {
                    fullMessage = sb.ToString();
                }
                _expectedSequence.TryRemove(guid, out _);
                return Result.Complete;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _partialMessages.Clear();
                _expectedSequence.Clear();
            }
        }
    }
}
