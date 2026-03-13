namespace TCTVocabulary.Realtime
{
    public sealed class UserPresenceChange
    {
        public int UserId { get; init; }
        public int ConnectionCount { get; init; }
        public bool BecameOnline { get; init; }
        public bool WentOffline { get; init; }
    }

    public static class UserPresenceTracker
    {
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<int, HashSet<string>> ConnectionsByUserId = new();
        private static readonly Dictionary<string, int> UserIdByConnectionId = new();

        public static UserPresenceChange AddConnection(int userId, string connectionId)
        {
            lock (SyncRoot)
            {
                if (!ConnectionsByUserId.TryGetValue(userId, out var connections))
                {
                    connections = new HashSet<string>(StringComparer.Ordinal);
                    ConnectionsByUserId[userId] = connections;
                }

                var wasOffline = connections.Count == 0;
                connections.Add(connectionId);
                UserIdByConnectionId[connectionId] = userId;

                return new UserPresenceChange
                {
                    UserId = userId,
                    ConnectionCount = connections.Count,
                    BecameOnline = wasOffline && connections.Count == 1,
                    WentOffline = false
                };
            }
        }

        public static UserPresenceChange? RemoveConnection(string connectionId)
        {
            lock (SyncRoot)
            {
                if (!UserIdByConnectionId.Remove(connectionId, out var userId))
                {
                    return null;
                }

                if (!ConnectionsByUserId.TryGetValue(userId, out var connections))
                {
                    return new UserPresenceChange
                    {
                        UserId = userId,
                        ConnectionCount = 0,
                        BecameOnline = false,
                        WentOffline = true
                    };
                }

                connections.Remove(connectionId);
                var connectionCount = connections.Count;

                if (connectionCount == 0)
                {
                    ConnectionsByUserId.Remove(userId);
                }

                return new UserPresenceChange
                {
                    UserId = userId,
                    ConnectionCount = connectionCount,
                    BecameOnline = false,
                    WentOffline = connectionCount == 0
                };
            }
        }

        public static bool IsUserOnline(int userId)
        {
            lock (SyncRoot)
            {
                return ConnectionsByUserId.TryGetValue(userId, out var connections)
                    && connections.Count > 0;
            }
        }

        public static IReadOnlyCollection<int> GetOnlineUserIds()
        {
            lock (SyncRoot)
            {
                return ConnectionsByUserId.Keys.ToArray();
            }
        }
    }
}
