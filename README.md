# dotnet-channels-benchmarks

[![build](https://github.com/pawla-homsi/dotnet-channels-benchmarks/actions/workflows/build.yml/badge.svg)](https://github.com/pawla-homsi/dotnet-channels-benchmarks/actions/workflows/build.yml)

Throughput and memory measurements of `System.Threading.Channels` in .NET, using BenchmarkDotNet over 10 million messages per run.

## Why

Before using channels on a production request path I wanted to know their cost: how much they allocate, how synchronous and asynchronous reads compare, and what happens when the consumer falls behind.

## Findings

Full write-up with result tables: [docs/article.md](docs/article.md).

- **The channel adds almost no allocation of its own.** With `int` messages, allocation stayed under 100 bytes per run, because `ChannelReader` and `ChannelWriter` return `ValueTask`.
- **With objects, allocation equals the payload.** 10 million 40-byte objects allocated 381 MB, which is 10,000,000 × 40 bytes.
- **Asynchronous reads are slower than synchronous ones.** Reading before writing forces every read to wait for a writer.
- **A consumer reading at half the write rate is the real risk.** The unbounded channel grows, garbage collection work rises, and the run took around 10 minutes. That is the case for a bounded channel or a durable queue.

Benchmark code adapted from Stephen Toub's [An Introduction to System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/).

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Benchmarks must run in Release mode.

```bash
git clone https://github.com/pawla-homsi/dotnet-channels-benchmarks.git
cd dotnet-channels-benchmarks
dotnet run -c Release --project NetChannelsBenchmark
```

Pick a benchmark from the menu, or run one directly:

```bash
dotnet run -c Release --project NetChannelsBenchmark -- --filter "*ObjectChannelBenchmark*"
```

## Benchmarks

| Class | Payload | Cases |
|---|---|---|
| `ChannelsBenchmark` | `int` | write then read, read then write |
| `ObjectChannelBenchmark` | 40-byte object | write then read, read then write |
| `BigObjectChannelBenchmark` | object with seven string fields | write then read, read then write |

## Tech

C#, .NET 10, BenchmarkDotNet.

## Roadmap

- [ ] Re-run on .NET 10 and publish the new tables next to the 2022 results
- [ ] Bounded vs unbounded, and `SingleReader` / `SingleWriter` options
- [ ] Compare against `BlockingCollection<T>` and `ConcurrentQueue<T>` with polling

## License

MIT
