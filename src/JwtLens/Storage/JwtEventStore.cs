using JwtLens.Models;

namespace JwtLens.Storage;

/// <summary>
/// Thread-safe ring buffer store for JWT events.
/// </summary>
public sealed class JwtEventStore
{
    private readonly object _lock = new();
    private JwtEvent[] _buffer;
    private int _head;
    private int _count;
    private long _totalCaptured;

    /// <summary>
    /// Creates a new JwtEventStore with the specified capacity.
    /// </summary>
    public JwtEventStore(int capacity = 1000)
    {
        _buffer = new JwtEvent[capacity];
    }

    /// <summary>Current number of stored events.</summary>
    public int Count
    {
        get { lock (_lock) return _count; }
    }

    /// <summary>Total number of events ever captured.</summary>
    public long TotalCaptured
    {
        get { lock (_lock) return _totalCaptured; }
    }

    /// <summary>Adds an event to the store.</summary>
    public void Add(JwtEvent evt)
    {
        lock (_lock)
        {
            int index = (_head + _count) % _buffer.Length;
            if (_count == _buffer.Length)
            {
                // Overwrite oldest
                _buffer[_head] = evt;
                _head = (_head + 1) % _buffer.Length;
            }
            else
            {
                _buffer[index] = evt;
                _count++;
            }
            _totalCaptured++;
        }
    }

    /// <summary>Gets all stored events in order.</summary>
    public List<JwtEvent> GetAll()
    {
        lock (_lock)
        {
            var result = new List<JwtEvent>(_count);
            for (int i = 0; i < _count; i++)
            {
                result.Add(_buffer[(_head + i) % _buffer.Length]);
            }
            return result;
        }
    }

    /// <summary>Gets the most recently added event.</summary>
    public JwtEvent? GetLast()
    {
        lock (_lock)
        {
            if (_count == 0) return null;
            int lastIndex = (_head + _count - 1) % _buffer.Length;
            return _buffer[lastIndex];
        }
    }

    /// <summary>Clears all stored events and resets counters.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
            _totalCaptured = 0;
        }
    }
}
