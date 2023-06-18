// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    internal abstract class BufferedProvider<ItemType>
    {
        private readonly ItemType[] _buffer;

        private int _start; // the index of the first item in the circular buffer

        private int _end; // the index of the last item in the circular buffer

        private int _count; // the number of items that are available in the circular buffer (in case _start == _end, the circular buffer can be completely empty or completely full)

        protected BufferedProvider(int bufferLength)
        {
            _buffer = new ItemType[bufferLength];

            _start = _end = _count = 0;
        }

        public ItemType GetNextItem()
        {
            if (!IsItemAvailableInBuffer(0))
            {
                FillBuffer();
            }

            Debug.Assert(IsItemAvailableInBuffer(0));

            ItemType result = _buffer[_start];

            _start = (_start + 1) % _buffer.Length;

            _count--;

            return result;
        }

        public ItemType PeekNextItem(int delta)
        {
            if (delta >= _buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "Cannot peek that far ahead");
            }

            if (!IsItemAvailableInBuffer(delta))
            {
                FillBuffer();
            }

            Debug.Assert(IsItemAvailableInBuffer(delta));

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

            ItemType[] items = GetNextItems(itemsToRead);

            foreach (ItemType item in items)
            {
                _buffer[_end] = item;

                _end = (_end + 1) % _buffer.Length;

                _count++;
            }
        }

        protected abstract ItemType[] GetNextItems(int count);
    }

    internal class EnumeratedBufferedProvider<ItemType> : BufferedProvider<ItemType>
    {
        private readonly IEnumerator<ItemType> _enumerator;

        private readonly ItemType _missingItemSentinel;

        public EnumeratedBufferedProvider(IEnumerable<ItemType> sequence, ItemType missingItemSentinel, int bufferLength)
            : base(bufferLength)
        {
            _enumerator = sequence.GetEnumerator();

            _missingItemSentinel = missingItemSentinel;
        }

        sealed protected override ItemType[] GetNextItems(int count)
        {
            List<ItemType> result = new List<ItemType>(count);
            for (int i = 0; i < count; i++)
            {
                bool hasNext = _enumerator.MoveNext();
                if (hasNext)
                {
                    result.Add(_enumerator.Current);
                }
                else
                {
                    result.Add(_missingItemSentinel); // might reuse the same instance
                }
            }
            return result.ToArray();
        }

        //public ItemType[] TestGetNextItems(int count)
        //{
        //    return GetNextItems(count);
        //}
    }

    internal abstract class ByteStreamBufferedProvider : BufferedProvider<byte>
    {
        private long _availableLength;

        private int _index;

        protected ByteStreamBufferedProvider(long availableLength, int startIndex, int bufferLength)
            : base(bufferLength)
        {
            _availableLength = availableLength;

            _index = startIndex;
        }

        sealed protected override byte[] GetNextItems(int count)
        {
            int remainingItems = Math.Max(0, (int)_availableLength - _index);

            if (count <= remainingItems)
            {
                int startIndex = _index;
                _index += count;
                return GetBytes(startIndex, count);
            }
            else
            {
                // return all remaining items, adding zero bytes for the missing items
                List<byte> result = new List<byte>();
                if (remainingItems > 0)
                {
                    int startIndex = _index;
                    _index += count;
                    result.AddRange(GetBytes(startIndex, remainingItems));
                }
                result.AddRange(new byte[count - remainingItems]);
                return result.ToArray();
            }
        }

        protected abstract byte[] GetBytes(int index, int count);

        //public byte[] TestGetNextItems(int count)
        //{
        //    return GetNextItems(count);
        //}
    }

    internal class IndexedReaderByteProvider : ByteStreamBufferedProvider
    {
        private readonly IndexedReader _reader;

        public IndexedReaderByteProvider(IndexedReader reader, int startIndex, int bufferLength)
            : base(reader.Length, startIndex, bufferLength)
        {
            _reader = reader;
        }

        protected override byte[] GetBytes(int index, int count)
        {
            return _reader.GetBytes(index, count);
        }
    }

    internal class StringByteProvider2 : ByteStreamBufferedProvider
    {
        private readonly byte[] _input;

        public StringByteProvider2(string input, int startIndex, int bufferLength)
            : base(input.Length, startIndex, bufferLength)
        {
            // input string must only contain 1-byte characters

            _input = input.ToCharArray().Select(x => (byte)x).ToArray();
        }

        protected override byte[] GetBytes(int index, int count)
        {
            return _input.Skip(index).Take(count).ToArray();
        }
    }
}
