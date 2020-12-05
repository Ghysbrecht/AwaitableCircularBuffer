using NUnit.Framework;
using System.Linq;

namespace AwaitableCircularBuffer
{
    [TestFixture]
    public class UnitTests
    {
        [Test]
        public void Test_CalculateNextIndex()
        {
            // Simple start case
            Assert.AreEqual(2, BufferLogicHelper.CalculateNextIndex(0, 2, 10, out var isOverFlowed));
            Assert.IsFalse(isOverFlowed);

            // Fully fill the end
            Assert.AreEqual(0, BufferLogicHelper.CalculateNextIndex(7, 3, 10, out isOverFlowed));
            Assert.IsTrue(isOverFlowed);

            // Overflow
            Assert.AreEqual(2, BufferLogicHelper.CalculateNextIndex(7, 5, 10, out isOverFlowed));
            Assert.IsTrue(isOverFlowed);
        }

        [Test]
        public void Test_WillDataBeOverwritten()
        {
            Assert.IsFalse(BufferLogicHelper.WillDataBeOverwritten(10, 0, 10));
            Assert.IsTrue(BufferLogicHelper.WillDataBeOverwritten(5, 6, 10));
            Assert.IsFalse(BufferLogicHelper.WillDataBeOverwritten(5, 5, 10));
        }

        [Test]
        public void Test_CalculateUsedCapacity()
        {
            Assert.AreEqual(0, BufferLogicHelper.CalculateUsedCapacity(true, 0, 0, 10));
            Assert.AreEqual(10, BufferLogicHelper.CalculateUsedCapacity(false, 0, 0, 10));
            Assert.AreEqual(9, BufferLogicHelper.CalculateUsedCapacity(false, 0, 9, 10));
            Assert.AreEqual(4, BufferLogicHelper.CalculateUsedCapacity(false, 5, 9, 10));
            Assert.AreEqual(10, BufferLogicHelper.CalculateUsedCapacity(false, 9, 9, 10));
            Assert.AreEqual(0, BufferLogicHelper.CalculateUsedCapacity(true, 9, 9, 10));
            Assert.AreEqual(2, BufferLogicHelper.CalculateUsedCapacity(false, 9, 1, 10));
            Assert.AreEqual(9, BufferLogicHelper.CalculateUsedCapacity(false, 9, 8, 10));
        }

        [Test]
        public void Test_SetAndGet()
        {
            var buffer = new AwaitableCircularBuffer<double>(10);
            var testArray = new[] {1.111, 2.222, 3.333, 4.444, 5.555, 6.666, 7.777, 8.888, 9.999, 10.10};
            var notifyEvent = buffer.GetNotifyEvent(4);

            // Write the first 2 items, event should not be set
            buffer.Put(testArray.Take(2).ToArray());
            Assert.IsFalse(notifyEvent.IsSet);

            // Write 2 more items, event should be set
            buffer.Put(testArray.Skip(2).Take(2).ToArray());
            Assert.IsTrue(notifyEvent.IsSet);

            // Read the four items, they should match the four first items from the test array.
            // Event should be reset.
            var readData = buffer.Get();
            Assert.AreEqual(4, readData.Length);
            Assert.IsTrue(testArray.Take(4).SequenceEqual(readData));
            Assert.IsFalse(notifyEvent.IsSet);

            // Write six more items
            buffer.Put(testArray.Skip(4).Take(6).ToArray());
            Assert.IsTrue(notifyEvent.IsSet);

            // Get four items & check if they are correct & that the event is not set
            readData = buffer.Get();
            Assert.AreEqual(4, readData.Length);
            Assert.IsTrue(testArray.Skip(4).Take(4).SequenceEqual(readData));
            Assert.IsFalse(notifyEvent.IsSet);
            
            // Try to add 9 items, this should not be done as the there is not enough room
            buffer.Put(testArray.Take(9).ToArray());
            Assert.IsFalse(notifyEvent.IsSet);
            Assert.AreEqual(9, buffer.LostData);
        }
    }
}
