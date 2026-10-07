# .Net Channels Performance - Benchmarks
## Introduction
In the previous article [this article](https://github.com/pawla-homsi/dotnet-channels-guide) we talked about channels, what are they and how to create a channel, write to it and read from it.
In this article let’s talk about the more interesting stuff, their Performance!

## So, What about Performance?
This is the interesting part for me, all of the previous introduction and how to use Channels is interesting and everything but the real question is what about performance?

How much load can we put on these channels? If we’re going to use a bounded channel what is the limit?

As much as .NET channels sound simple, but behind the scenes there is a complicated and pretty efficient written code. Let’s check that together and try some benchmarks to see how efficient channels are. I’ve taken the code samples from [this amazing blog](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) written by Stephen Toub and wanted to try them myself.

In the first benchmark, we’re trying to write then read an integer value 10 millions time, but in here, the write is happening before the read meaning that the read will always have an element to be read in the channel, thus the read will not have to wait and will always complete synchronously.

```
private readonly Channel<int> s_channel = Channel.CreateUnbounded<int>();

[Benchmark]
public async Task WriteThenRead()
{
   ChannelWriter<int> writer = s_channel.Writer;
   ChannelReader<int> reader = s_channel.Reader;
   for (int i = 0; i < 10_000_000; i++)
   {
      writer.TryWrite(i);
      await reader.ReadAsync();
   }
}
```

In the second benchmark, we will try to read then write to our channel, the same 10 millions integer elements. In this benchmark the read is completed asynchronously because by the time we’re reading, there’s still no element written in the channel, so we have to wait till the write is done in order to be able to read an element

```
private readonly Channel<int> s_channel = Channel.CreateUnbounded<int>();

[Benchmark]
public async Task ReadThenWrite()
{
   ChannelWriter<int> writer = s_channel.Writer;
   ChannelReader<int> reader = s_channel.Reader;
   for (int i = 0; i < 10_000_000; i++)
   {
      ValueTask<int> vt = reader.ReadAsync();
      writer.TryWrite(i);
      await vt;
   }
}
```

As shown below in the results table, when reading asynchronously the performance is a bit slower which makes sense because the read takes time till it’s notified that there are new elements written to the channel.

The memory allocation in both cases did not exceed 100 Bytes, although this number was varying in different runs but it was close most of the times to these values. Based on these findings we can see that the memory allocations is actually minimal in the .NET channel implementations.

![Integers benchmark results](../NetChannelsBenchmark/assets/firstbenckmark.png)

How is this low allocation is actually achieved?

If we take a look inside ChannelReader we can see that its methods return ValueTask instead of Task, which is internally a struct not a class and therefore it will be allocated on the stack not on the heap. Since we’re using integers and the channel is using ValueTask, this should explain the no allocated memory results above.

```
public abstract class ChannelReader<T>{
   public virtual ValueTask<T> ReadAsync(CancellationToken cancellationToken = default);
   public abstract ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default);
}
```
Same for ChannelWriter

```
public abstract class ChannelWriter<T>{
   public abstract ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default);
   public virtual ValueTask WriteAsync(T item, CancellationToken cancellationToken = default);
}
```


However, I wanted to go a step further in this benchmark and try to write/read an object instead of just an integer, so I went ahead and created an object with a size of exactly 40 Bytes, then tried the same above benchmarks on it.


```
private readonly Channel<object> s_channel = Channel.CreateUnbounded<object>();
[Benchmark]
public async Task WriteThenRead()
{
   ChannelWriter<object> writer = s_channel.Writer;
   ChannelReader<object> reader = s_channel.Reader;
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
```

Then I tried reading the object from the channel before writing to it, to test reading asynchronously

```
private readonly Channel<object> s_channel = Channel.CreateUnbounded<object>();
[Benchmark]
public async Task ReadThenWrite()
{
   ChannelWriter<object> writer = s_channel.Writer;
   ChannelReader<object> reader = s_channel.Reader;
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
````

The results I found were very interesting! and these numbers were consistent in every time I run the benchmark.

![Objects benchmark results](../NetChannelsBenchmark/assets/secondbenchmark.png)

So performance wise it’s still the same outcome reading asynchronously is a bit slower than reading synchronously.

Now about the memory allocation, as shown in the results table, both benchmarks have allocated exactly 381 MB for the 10M objects, if we did some quick calculations we see that 40 Bytes * 10M is exactly 381.47 MB of memory. Basically nothing other than our own objects allocations!

I have one more case to try, instead of reading the data immediately, I will read only half of the data and I will run the same benchmark with our 10 millions 40 Bytes objects
```
[Benchmark]
public async Task WriteThenRead()
{
   ChannelWriter<object> writer = s_channel.Writer;
   ChannelReader<object> reader = s_channel.Reader;
   for (int i = 0; i < 10_000_000; i++)
   {
      var obj = new
      {
         item1 = "123",
         item2 = "456",
         item3 = "789"
      };
      writer.TryWrite(obj);
      if(i % 2 == 0) await reader.ReadAsync();
   }
}
```
While running the benchmark the performance issue was obvious, it took about 10 minutes to finish, and here was the result of this benchmark

![benchmark results](../NetChannelsBenchmark/assets/lastbenchmark.png)

the memory allocation was the same, the actual memory usage was higher because it’s allocating the objects and keeping the objects that were not read in memory, and also the size of garbage collected data is higher here.

As we can see the performance degraded so much, and in this case it was kind of an issue, but in real life if we have 10 millions objects to store in a channel (queue) and our reading performance is half of our writing rate, maybe we should consider not using channels in the first place and go for other more reliable and scalable solutions.

## Summary
I find these results very interesting because this means that the channels code itself is almost allocation free! and most of the allocation that will happen in our code is very predictable and it completely depends on the size of our object.

Based on that, deciding the capacity of a channel should be more clear since we know the size of our object, the writing rate on our channel and the reading performance as well.

