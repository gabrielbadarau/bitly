using StackExchange.Redis;

namespace Bitly.WriteApi.Services;

public class RedisCodeGenerator(IConnectionMultiplexer redis)
{
    private const string CounterKey = "shorturl:counter";
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int BatchSize = 1000;

    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private long _nextValue;
    private long _batchEnd = -1;

    public async Task<string> NextCodeAsync()
    {
        await _batchLock.WaitAsync();
        try
        {
            if (_nextValue > _batchEnd)
            {
                var batchEnd = await redis.GetDatabase().StringIncrementAsync(CounterKey, BatchSize);
                _batchEnd = batchEnd;
                _nextValue = batchEnd - BatchSize + 1;
            }

            var value = _nextValue;
            _nextValue++;
            return Encode(value);
        }
        finally
        {
            _batchLock.Release();
        }
    }

    private static string Encode(long value)
    {
        if (value == 0)
        {
            return Base62Alphabet[0].ToString();
        }

        var chars = new Stack<char>();
        while (value > 0)
        {
            chars.Push(Base62Alphabet[(int)(value % 62)]);
            value /= 62;
        }

        return new string(chars.ToArray());
    }
}
