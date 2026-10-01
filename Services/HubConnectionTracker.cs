namespace SignalR_Demo.Services;

public interface IHubConnectionTracker
{
    void Track(Guid userId, string connectionId);
    void Untrack(Guid userId, string connectionId);
    IReadOnlyList<string> GetConnectionIds(Guid userId);
}

public sealed class HubConnectionTracker : IHubConnectionTracker
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, HashSet<string>> connectionsByUser = [];

    public void Track(Guid userId, string connectionId)
    {
        lock (sync)
        {
            if (!connectionsByUser.TryGetValue(userId, out var connections))
            {
                connections = [];
                connectionsByUser.Add(userId, connections);
            }

            connections.Add(connectionId);
        }
    }

    public void Untrack(Guid userId, string connectionId)
    {
        lock (sync)
        {
            if (!connectionsByUser.TryGetValue(userId, out var connections))
                return;

            connections.Remove(connectionId);
            if (connections.Count == 0)
                connectionsByUser.Remove(userId);
        }
    }

    public IReadOnlyList<string> GetConnectionIds(Guid userId)
    {
        lock (sync)
        {
            return connectionsByUser.TryGetValue(userId, out var connections)
                ? connections.ToArray()
                : [];
        }
    }
}