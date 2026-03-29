using TCTEnglish.ViewModels.AI;

namespace TCTEnglish.Realtime
{
    public sealed class AiStreamStartedMessage
    {
        public Guid ConversationId { get; init; }
        public string StreamId { get; init; } = string.Empty;

        public static AiStreamStartedMessage Create(Guid conversationId, string streamId)
        {
            return new AiStreamStartedMessage
            {
                ConversationId = conversationId,
                StreamId = streamId
            };
        }
    }

    public sealed class AiStreamChunkMessage
    {
        public Guid ConversationId { get; init; }
        public string StreamId { get; init; } = string.Empty;
        public string Chunk { get; init; } = string.Empty;
        public int ChunkIndex { get; init; }

        public static AiStreamChunkMessage Create(Guid conversationId, string streamId, string chunk, int chunkIndex)
        {
            return new AiStreamChunkMessage
            {
                ConversationId = conversationId,
                StreamId = streamId,
                Chunk = chunk,
                ChunkIndex = chunkIndex
            };
        }
    }

    public sealed class AiStreamCompletedMessage
    {
        public Guid ConversationId { get; init; }
        public string StreamId { get; init; } = string.Empty;
        public ChatUsageDto Usage { get; init; } = new(0, 0, 0, string.Empty);
        public ChatMetadataDto Metadata { get; init; } = new(string.Empty, 0);

        public static AiStreamCompletedMessage Create(
            Guid conversationId,
            string streamId,
            ChatUsageDto usage,
            ChatMetadataDto metadata)
        {
            return new AiStreamCompletedMessage
            {
                ConversationId = conversationId,
                StreamId = streamId,
                Usage = usage,
                Metadata = metadata
            };
        }
    }

    public sealed class AiStreamFailedMessage
    {
        public Guid ConversationId { get; init; }
        public string? StreamId { get; init; }
        public string ErrorCode { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int? RetryAfterSeconds { get; init; }

        public static AiStreamFailedMessage Create(
            Guid conversationId,
            string? streamId,
            string errorCode,
            string message,
            int? retryAfterSeconds = null)
        {
            return new AiStreamFailedMessage
            {
                ConversationId = conversationId,
                StreamId = streamId,
                ErrorCode = errorCode,
                Message = message,
                RetryAfterSeconds = retryAfterSeconds
            };
        }
    }
}

