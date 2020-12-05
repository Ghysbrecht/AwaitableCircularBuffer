using System;
using System.Threading;
using AwaitableCircularBuffer;

namespace BufferTesterConsoleApp
{
    public class Tester
    {
        private readonly ManualResetEventSlim _isShuttingDown;

        public Tester()
        {
            _isShuttingDown = new ManualResetEventSlim(false);
        }

        public void Start()
        {
            const int writeChunkSize = 16384;
            const int readChunkSize = 4096;
            var buffer = new AwaitableCircularBuffer<int>(writeChunkSize * 4);

            // Assign a chunk with random value, we will use the same chunk every time
            Console.WriteLine($"Initializing chunk (size = {writeChunkSize})");
            var chunk = new int[writeChunkSize];
            var random = new Random();
            for (int i = 0; i < writeChunkSize; i++)
            {
                chunk[i] = random.Next();
            }

            // Stats
            long writtenSamples = 0;
            long readSamples = 0;

            // Create & start threads
            var readThread = new Thread(() => ReadUntilSignaled(buffer, ref readSamples, readChunkSize));
            var writeThread = new Thread(() => WriteUntilSignaled(buffer, ref writtenSamples, chunk));

            readThread.Start();
            writeThread.Start();

            long previousWrite = 0;
            long previousRead = 0;
            while (!_isShuttingDown.IsSet)
            {
                Thread.Sleep(1_000);

                Console.Clear();

                var currentWritten = writtenSamples;
                var currentRead = readSamples;

                // Output total samples
                Console.WriteLine($"Total samples written: {currentWritten:N0}");
                Console.WriteLine($"Total samples read:    {currentRead:N0}");

                // Output samples per second
                var writtenSamplesPerSecond = currentWritten - previousWrite;
                var readSamplesPerSecond = currentRead - previousRead;
                Console.WriteLine($"Samples/second written: {writtenSamplesPerSecond:N0}");
                Console.WriteLine($"Samples/second read:    {readSamplesPerSecond:N0}");

                // Display how many data is lost
                var lostCalculateSamples = currentWritten - currentRead;
                var lostBufferSamples = buffer.LostChunks * writeChunkSize;
                Console.WriteLine($"Lost samples calculated: {lostCalculateSamples} ({(double)lostCalculateSamples / currentWritten * 100.0:F} %)");
                Console.WriteLine($"Lost samples buffer:     {lostBufferSamples} ({(double)lostBufferSamples / currentWritten * 100.0:F} %)");

                previousWrite = writtenSamples;
                previousRead = currentRead;
            }

            // Wait until both threads are shut down
            readThread.Join();
            writeThread.Join();
            Console.WriteLine("All threads terminated, goodbye!");
        }

        public void WriteUntilSignaled(AwaitableCircularBuffer<int> buffer, ref long writtenSamples, int[] chunkToWrite)
        {
            Console.WriteLine("Starting WRITE thread...");

            while (!_isShuttingDown.IsSet)
            {
                buffer.Put(chunkToWrite);
                writtenSamples += chunkToWrite.Length;
            }

            Console.WriteLine("Shutting down WRITE thread...");
        }

        public void ReadUntilSignaled(AwaitableCircularBuffer<int> buffer, ref long readSamples, int chunkSizeToRead)
        {
            Console.WriteLine($"Starting READ thread... (Chunk size: {chunkSizeToRead})");
            var hasDataEvent = buffer.GetNotifyEvent(chunkSizeToRead);

            while (!_isShuttingDown.IsSet)
            {
                // Wait until there is data to read
                if (!hasDataEvent.Wait(1_000))
                {
                    // No data received for a while, check if we need to shut down
                    continue;
                }

                var newData = buffer.Get();
                readSamples+= newData.Length;
            }

            Console.WriteLine("Shutting down READ thread...");
        }

        public void ShutDown(object sender, EventArgs e)
        {
            Console.WriteLine("Process exit requested, shutting down threads...");
            _isShuttingDown.Set();
        }
    }
}
