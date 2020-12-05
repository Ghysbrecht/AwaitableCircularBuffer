# AwaitableCircularBuffer
Simple C# circular buffer with a wait event when new data is available

This is a simple circular buffer made in C#. You can request a `ManualResetEventSlim` from the buffer that will be set when enough data is available. 
See the included performance test app for an example usage.
Feel free to use this if you want. I cannot guarantee that it is 100% reliable, so do your own testing.
