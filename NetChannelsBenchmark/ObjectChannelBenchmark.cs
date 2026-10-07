using System.Threading.Tasks;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace NetChannelsBenchmark
{
    [MemoryDiagnoser]
    public class ObjectChannelBenchmark
    {
        private readonly Channel<object> _channel = Channel.CreateUnbounded<object>();

        [Benchmark]
        public async Task WriteThenRead()
        {
            ChannelWriter<object> writer = _channel.Writer;
            ChannelReader<object> reader = _channel.Reader;
            for (int i = 0; i < 10_000_000; i++)
            {
                var obj = new
                {
                    item1 = "123",
                    item2 = "456",
                    item3 = "789"
                };
                writer.TryWrite(obj);
                await reader.ReadAsync();
            }
        }

        [Benchmark]
        public async Task ReadThenWrite()
        {
            ChannelWriter<object> writer = _channel.Writer;
            ChannelReader<object> reader = _channel.Reader;
            for (int i = 0; i < 10_000_000; i++)
            {
                var obj = new
                {
                    item1 = "123",
                    item2 = "456",
                    item3 = "789"
                };
                ValueTask<object> vt = reader.ReadAsync();
                writer.TryWrite(obj);
                await vt;
            }
        }

    }
}
