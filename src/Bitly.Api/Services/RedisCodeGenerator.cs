using StackExchange.Redis;

namespace Bitly.Api.Services;

public class RedisCodeGenerator(IConnectionMultiplexer redis)
{
    private const string CounterKey = "shorturl:counter";
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public async Task<string> NextCodeAsync()
    {
        var counter = await redis.GetDatabase().StringIncrementAsync(CounterKey);
        return Encode(counter);
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
