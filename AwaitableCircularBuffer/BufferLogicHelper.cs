namespace AwaitableCircularBuffer
{
    public class BufferLogicHelper
    {
        /// <summary>
        /// Checks whether existing, unread data will be overwritten for the next write
        /// </summary>
        public static bool WillDataBeOverwritten(int lengthOfDataToWrite, int currentlyUsedCapacity, int totalCapacity)
        {
            return (currentlyUsedCapacity + lengthOfDataToWrite) > totalCapacity;
        }

        public static int CalculateUsedCapacity(bool isEmpty, int currentHeadIndex, int currentTailIndex, int totalCapacity)
        {
            if (isEmpty)
            {
                return 0;
            }

            if (currentTailIndex > currentHeadIndex)
            {
                // Current data is not overflowed
                return currentTailIndex - currentHeadIndex;
            }
            else
            {
                // Current data is overflowed, take inverse of non-overflowed case
                return totalCapacity - (currentHeadIndex - currentTailIndex);
            }
        }

        /// <summary>
        /// Returns what the next head/tail index will be when a read/write operation is done.
        /// <paramref name="isOverflowed"/> is true when the next index wrapped around back to zero.
        /// </summary>
        public static int CalculateNextIndex(int currentIndex, int lengthOfData, int totalCapacity, out bool isOverflowed)
        {
            var nextId = currentIndex + lengthOfData;
            if (nextId >= totalCapacity)
            {
                isOverflowed = true;
                return nextId - totalCapacity;
            }

            isOverflowed = false;
            return nextId;
        }
    }
}
