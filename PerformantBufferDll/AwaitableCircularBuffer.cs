using System;
using System.Threading;

namespace AwaitableCircularBuffer
{
    /// <summary>
    /// Non thread-safe in the sense where multiple threads are writing or reading at the same time. A single thread can write, and another one can read.
    /// </summary>
    public class AwaitableCircularBuffer<T>
    {
        #region Private Fields
        /// <summary>
        /// Main lock object to lock access to the main private fields
        /// </summary>
        private readonly object _lockObj = new object();

        /// <summary>
        /// Contains the index where new data starts
        /// </summary>
        private int _head;

        /// <summary>
        /// Contains the index of where the next data can be written to
        /// </summary>
        private int _tail;

        /// <summary>
        /// Is true when there is no data in this buffer.
        /// Used to circumvent the ambiguity where _tail == _head can mean empty, or full.
        /// </summary>
        private bool _isEmpty = true;

        /// <summary>
        /// Threshold of when the notify event should be set/reset
        /// </summary>
        private int _notifyThreshold;

        /// <summary>
        /// Will be set when the amount of available data matches or exceeds <see cref="_notifyThreshold"/>
        /// </summary>
        private readonly ManualResetEventSlim _notifyEvent;

        /// <summary>
        /// Determines whether a threshold is set, and the the <see cref="_notifyEvent"/> can be awaited on
        /// </summary>
        private bool _notifyEventEnabled;

        /// <summary>
        /// Contains how much items can be stored in the buffer
        /// </summary>
        private readonly int _capacity;

        /// <summary>
        /// Main storage array that will be initialized in the constructor
        /// </summary>
        private readonly T[] _storage;
        #endregion

        #region Public Properties
        /// <summary>
        /// Will increment each time a chunk of data could not be written
        /// </summary>
        public long LostChunks { get; private set; }
        #endregion

        public AwaitableCircularBuffer(int capacity)
        {
            _capacity = capacity;
            _storage = new T[_capacity];
            _notifyEvent = new ManualResetEventSlim(false);
        }

        /// <summary>
        /// Returns the notify event that can be waited on. It will be triggered when enough data is available to do a get with the given threshold size.
        /// </summary>
        public ManualResetEventSlim GetNotifyEvent(int threshold)
        {
            lock (_lockObj)
            {
                _notifyThreshold = threshold;
                _notifyEventEnabled = true;
            }

            return _notifyEvent;
        }

        public T[] Get()
        {
            bool isOverflowed;
            int nextHead;
            lock (_lockObj)
            {
                if (BufferLogicHelper.CalculateUsedCapacity(_isEmpty, _head, _tail, _capacity) < _notifyThreshold)
                {
                    // We do not have enough data
                    throw new Exception("A get should never be done when there is not enough available data! Use the NotifyEvent.");
                }

                nextHead = BufferLogicHelper.CalculateNextIndex(_head, _notifyThreshold, _capacity, out isOverflowed);
            }

            var getValues = new T[_notifyThreshold];
            if (isOverflowed)
            {
                // Data spans across the end and beginning of the storage array, we need to do two copies
                var itemsToReadFromEnd = _capacity - _head;
                Array.Copy(_storage, _head, getValues, 0, itemsToReadFromEnd);

                var itemsToReadFromBeginning = _notifyThreshold - itemsToReadFromEnd;
                Array.Copy(_storage, 0, getValues, itemsToReadFromEnd, itemsToReadFromBeginning);
            }
            else
            {
                Array.Copy(_storage, _head, getValues, 0, _notifyThreshold);
            }

            lock (_lockObj)
            {
                _head = nextHead;

                // Check if we just read the last available data
                _isEmpty = _head == _tail;

                // Check if we need to reset the event
                if (_notifyEventEnabled && _notifyEvent.IsSet && BufferLogicHelper.CalculateUsedCapacity(_isEmpty, _head, _tail, _capacity) < _notifyThreshold)
                {
                    // There is not enough data anymore, reset the event
                    _notifyEvent.Reset();
                }
            }

            return getValues;
        }

        /// <summary>
        /// Not allowed to set data larger than the buffer size
        /// </summary>
        public void Put(T[] dataToSet)
        {
            var sourceDataLength = dataToSet.Length;
            if(sourceDataLength > _capacity)
            {
                throw new ArgumentException("Amount of data that is trying to be set is larger than the capacity of this buffer");
            }

            bool isOverflowed;
            int nextTail;
            lock (_lockObj)
            {
                // Calculate the amount of data (including the data being read at the moment, if a read is ongoing)
                var capacityInUse = BufferLogicHelper.CalculateUsedCapacity(_isEmpty, _head, _tail, _capacity);
                if (BufferLogicHelper.WillDataBeOverwritten(sourceDataLength, capacityInUse, _capacity))
                {
                    // We just throw away a chunk if there is no room
                    LostChunks++;
                }

                // Calculate the next tail, this way we also know whether we are overflowing
                nextTail = BufferLogicHelper.CalculateNextIndex(_tail, sourceDataLength, _capacity, out isOverflowed);
            }

            if (isOverflowed)
            {
                // Data spans across the end and beginning of the storage array, we need to do two copies
                var itemsToWriteAtEnd = _capacity - _tail;
                Array.Copy(dataToSet, 0, _storage, _tail, itemsToWriteAtEnd);

                var itemsToWriteAtBeginning = sourceDataLength - itemsToWriteAtEnd;
                Array.Copy(dataToSet, itemsToWriteAtEnd, _storage, 0, itemsToWriteAtBeginning);
            }
            else
            {
                Array.Copy(dataToSet, 0, _storage, _tail, sourceDataLength);
            }

            lock (_lockObj)
            {
                _tail = nextTail;

                // We just added data, the storage can never be empty now
                _isEmpty = false;

                // Check if we need to set the event
                if (_notifyEventEnabled && !_notifyEvent.IsSet && BufferLogicHelper.CalculateUsedCapacity(_isEmpty, _head, _tail, _capacity) >= _notifyThreshold)
                {
                    // There is not enough data anymore, reset the event
                    _notifyEvent.Set();
                }
            }
        }
        /*
        /// <summary>
        /// Checks whether existing, unread data will be overwritten for the next write
        /// </summary>
        public bool WillDataBeOverwritten(int lengthOfDataToWrite, int currentlyUsedCapacity)
        {
            return (currentlyUsedCapacity + lengthOfDataToWrite) > _capacity;
        }

        public int CalculateUsedCapacity(bool isEmpty, int currentHeadIndex, int currentTailIndex)
        {
            if (isEmpty)
            {
                return 0;
            }

            if(currentTailIndex > currentHeadIndex)
            {
                // Current data is not overflowed
                return currentTailIndex - currentHeadIndex;
            }
            else
            {
                // Current data is overflowed, take inverse of non-overflowed case
                return _capacity - (currentHeadIndex - currentTailIndex);
            }
        }

        /// <summary>
        /// Returns what the next head/tail index would be when a read/write operation is done.
        /// <paramref name="isOverflowed"/> is true when the next index wrapped around back to zero.
        /// </summary>
        public int CalculateNextIndex(int currentIndex, int lengthOfData, out bool isOverflowed)
        {
            var nextId = currentIndex + lengthOfData;
            if(nextId >= _capacity)
            {
                isOverflowed = true;
                return nextId - _capacity;
            }

            isOverflowed = false;
            return nextId;
        }*/
    }
}
