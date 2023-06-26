// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public interface ItemProviderSource<ItemType>
    {
        ItemType DummyItem { get; }

        /// <summary>
        /// Returns as many items as possible, or an empty array if no more items are available.
        /// </summary>
        /// <param name="requestedCount">The maximum number of items to return, from the current position.</param>
        /// <returns>The next items, from the current position.</returns>
        ItemType[] GetNextItems(int requestedCount);

        int GetCurrentIndex(int itemsConsumed);
    }

    public abstract class ItemProvider<ItemType>
    {
        protected readonly ItemProviderSource<ItemType> ItemSource;

        protected int ItemsConsumed { get; private set; }

        public bool HasNextItem => IsItemAvailable(0);

        protected abstract bool IsItemAvailable(int delta);

        public int CurrentIndex => ItemSource.GetCurrentIndex(ItemsConsumed); // NOTE CurrentIndex is undefined when !HasNextItem

        protected ItemProvider(ItemProviderSource<ItemType> itemSource)
        {
            ItemSource = itemSource;

            ItemsConsumed = 0;
        }

        public ItemType GetNextItem()
        {
            ItemType result = HasNextItem ? DoGetNextItem() : ItemSource.DummyItem;

            ItemsConsumed++;

            return result;
        }

        public ItemType[] GetNextItems(int count)
        {
            List<ItemType> result = new List<ItemType>(count);

            for (int i = 0; i < count; i++)
            {
                result.Add(GetNextItem());
            }

            return result.ToArray();
        }

        public ItemType PeekNextItem(int delta)
        {
            if (delta < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "Cannot peek previous items");
            }

            ValidatePeekDelta(delta);

            if (!IsItemAvailable(delta))
            {
                return ItemSource.DummyItem;
            }

            return DoPeekNextItem(delta);
        }

        protected virtual void ValidatePeekDelta(int delta)
        {
            // always valid here
        }

        public void Consume(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _ = GetNextItem();
            }
        }

        protected abstract ItemType DoGetNextItem();

        protected abstract ItemType DoPeekNextItem(int delta);
    }

    public class BufferedItemProvider<ItemType> : ItemProvider<ItemType>
    {
        private readonly ItemType[] _buffer;

        private int _start; // the index of the first item in the circular buffer

        private int _end; // the index of the last item in the circular buffer

        private int _count; // the number of items that are available in the circular buffer (in case _start == _end, the circular buffer can be completely empty or completely full)

        private bool _endReached;

        protected override bool IsItemAvailable(int delta)
        {
            if (!_endReached && !IsItemAvailableInBuffer(delta))
            {
                FillBuffer();
            }

            return IsItemAvailableInBuffer(delta);
        }

        public BufferedItemProvider(ItemProviderSource<ItemType> itemSource, int bufferLength)
            : base(itemSource)
        {
            _buffer = new ItemType[bufferLength];

            _start = _end = _count = 0;

            _endReached = false;
        }

        protected override ItemType DoGetNextItem()
        {
            ItemType result = _buffer[_start];

            _start = (_start + 1) % _buffer.Length;

            _count--;

            return result;
        }

        protected override void ValidatePeekDelta(int delta)
        {
            if (delta >= _buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), $"Cannot peek that far ahead (max is {_buffer.Length - 1} items)");
            }
        }

        protected override ItemType DoPeekNextItem(int delta)
        {
            return _buffer[(_start + delta) % _buffer.Length];
        }

        private bool IsItemAvailableInBuffer(int delta)
        {
            return (delta + 1 <= _count);
        }

        private void FillBuffer()
        {
            int itemsToRead = _buffer.Length - _count;

            Debug.Assert(itemsToRead > 0);

            if (_endReached)
            {
                return;
            }

            ItemType[] items = ItemSource.GetNextItems(itemsToRead);

            foreach (ItemType item in items)
            {
                _buffer[_end] = item;

                _end = (_end + 1) % _buffer.Length;

                _count++;
            }

            if (items.Length < itemsToRead)
            {
                _endReached = true;
            }
        }
    }

    public class BoundedItemProvider<ItemType> : ItemProvider<ItemType>
    {
        private readonly ItemType[] _items;

        protected override bool IsItemAvailable(int delta)
        {
            return (ItemsConsumed + delta < _items.Length);
        }

        public BoundedItemProvider(ItemProviderSource<ItemType> itemSource, int requestedCount)
          : base(itemSource)
        {
            _items = itemSource.GetNextItems(requestedCount);
        }

        protected override ItemType DoGetNextItem()
        {
            return _items[ItemsConsumed];
        }

        protected override ItemType DoPeekNextItem(int delta)
        {
            return _items[ItemsConsumed + delta];
        }
    }

    public class EnumeratedItemProviderSource<ItemType> : ItemProviderSource<ItemType>
    {
        private readonly IEnumerator<ItemType> _enumerator;

        private bool _endReached;

        public ItemType DummyItem { get; }

        public int GetCurrentIndex(int itemsConsumed)
        {
            return itemsConsumed;
        }

        public EnumeratedItemProviderSource(IEnumerable<ItemType> sequence, ItemType dummyItem)
        {
            _enumerator = sequence.GetEnumerator();

            DummyItem = dummyItem; // reusing the same instance

            _endReached = false;
        }

        public ItemType[] GetNextItems(int requestedCount)
        {
            Debug.Assert(!_endReached);
            List<ItemType> result = new(requestedCount);
            for (var i = 0; i < requestedCount; i++)
            {
                var hasNext = _enumerator.MoveNext();
                if (hasNext)
                {
                    result.Add(_enumerator.Current);
                }
                else
                {
                    _endReached = true;
                    break;
                }
            }
            return result.ToArray();
        }
    }

    public enum ExtractionDirection
    {
        Forward = 1,
        Backward = -1,
    }

    public abstract class ByteStreamItemProviderSource : ItemProviderSource<byte>
    {
        private readonly ExtractionDirection _extractionDirection;

        private readonly long _availableLength;

        private readonly int _startOffset;

        private int _index; // the index of the next byte to be returned (can go above _availableLength when extracting forward, or below zero when extracting backward, indicating the end of input)

        public byte DummyItem => 0;

        public int GetCurrentIndex(int itemsConsumed)
        {
            return _startOffset + itemsConsumed * (int)_extractionDirection;
        }

        protected ByteStreamItemProviderSource(int startOffset, long availableLength, ExtractionDirection extractionDirection)
        {
            _extractionDirection = extractionDirection;

            _availableLength = availableLength;

            _index = _startOffset = _extractionDirection == ExtractionDirection.Forward ? startOffset : (int)availableLength - startOffset - 1;
        }

        public byte[] GetNextItems(int count)
        {
            return _extractionDirection == ExtractionDirection.Forward ? GetNextItemsForward(count) : GetNextItemsBackward(count);
        }

        protected abstract byte[] GetBytes(int index, int count);

        private byte[] GetNextItemsForward(int count)
        {
            int remainingItems = Math.Max(0, (int)_availableLength - _index);

            if (remainingItems < 1)
            {
                return new byte[] { };
            }
            else if (count <= remainingItems)
            {
                int startIndex = _index;
                _index += count;
                return GetBytes(startIndex, count);
            }
            else
            {
                // return all remaining items (length is < count)
                int startIndex = _index;
                _index += count;
                return GetBytes(startIndex, remainingItems);
            }
        }

        private byte[] GetNextItemsBackward(int count)
        {
            int remainingItems = Math.Max(0, _index + 1);

            if (remainingItems < 1)
            {
                return new byte[] { };
            }
            else if (count <= remainingItems)
            {
                int startIndex = _index - count + 1;
                _index -= count;
                return GetBytes(startIndex, count).Reverse().ToArray();
            }
            else
            {
                // return all remaining items (length is < count)
                int startIndex = _index - remainingItems + 1;
                _index -= count;
                return GetBytes(startIndex, remainingItems).Reverse().ToArray();
            }
        }
    }

    internal class IndexedReaderByteProviderSource : ByteStreamItemProviderSource
    {
        private readonly IndexedReader _reader;

        public IndexedReaderByteProviderSource(IndexedReader reader, int startIndex, ExtractionDirection extractionDirection)
            : base(startIndex, reader.Length, extractionDirection)
        {
            _reader = reader;
        }

        protected override byte[] GetBytes(int index, int count)
        {
            Debug.Assert(index >= 0);
            Debug.Assert(count > 0);

            byte[] result = _reader.GetBytes(index, count);

            Debug.Assert(result.Length == count);

            return result;
        }
    }

    public class StringByteProviderSource : ByteStreamItemProviderSource
    {
        private readonly byte[] _input;

        public StringByteProviderSource(string input, int startIndex, ExtractionDirection extractionDirection)
            : base(startIndex, input.Length, extractionDirection)
        {
            // input string must only contain 1-byte characters
            _input = input.ToCharArray().Select(x => (byte)x).ToArray();
        }

        protected override byte[] GetBytes(int index, int count)
        {
            Debug.Assert(index >= 0);
            Debug.Assert(count > 0);

            byte[] result = _input.Skip(index).Take(count).ToArray();

            Debug.Assert(result.Length == count);

            return result;
        }
    }
}
