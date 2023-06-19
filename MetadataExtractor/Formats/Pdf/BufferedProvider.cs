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

        protected void Consume(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GetNextItem();
            }
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

    internal enum ExtractionDirection
    {
        Forward,
        Backward,
    }

    internal abstract class ByteStreamBufferedProvider : BufferedProvider<byte>
    {
        private readonly ExtractionDirection _extractionDirection;

        private readonly long _availableLength;

        private int _index; // the index of the next byte to be returned (can go above _availableLength when extracting forward, or below zero when extracting backward, indicating the end of input)

        private int _bytesRead;

        public bool HasNextItem
        {
            get
            {
                int remainingItems = _extractionDirection == ExtractionDirection.Forward
                    ? Math.Max(0, (int)_availableLength - _index)
                    : Math.Max(0, _index + 1)
                ;

                return remainingItems > 0;
            }
        }

        public int BytesRead
        {
            get => _bytesRead;
        }

        protected ByteStreamBufferedProvider(long availableLength, int startIndex, int bufferLength, ExtractionDirection extractionDirection)
            : base(bufferLength)
        {
            _extractionDirection = extractionDirection;

            _availableLength = availableLength;

            _index = _extractionDirection == ExtractionDirection.Forward ? startIndex : (int)availableLength - 1;
        }

        public bool MatchMarker(string marker)
        {
            // marker should contain only 1-byte characters
            return MatchMarker(marker.ToCharArray().Select(x => (byte)x).ToArray());
        }

        public bool MatchMarker(byte[] marker)
        {
            for (int i = 0; i < marker.Length; i++)
            {
                if (marker[i] != PeekNextItem(i))
                {
                    return false;
                }
            }
            Consume(marker.Length);
            return true;
        }

        public bool MatchEndOfLineMarker()
        {
            if (PeekNextItem(0) == (byte)'\r' && PeekNextItem(1) == (byte)'\n')
            {
                Consume(2);
                return true;
            }
            else if (PeekNextItem(0) == (byte)'\r' || PeekNextItem(1) == (byte)'\n')
            {
                Consume(1);
                return true;
            }
            return false;
        }

        sealed protected override byte[] GetNextItems(int count)
        {
            return _extractionDirection == ExtractionDirection.Forward ? GetNextItemsForward(count) : GetNextItemsBackward(count);
        }

        protected abstract byte[] GetBytes(int index, int count);

        private byte[] GetNextItemsForward(int count)
        {
            int remainingItems = Math.Max(0, (int)_availableLength - _index);

            if (count <= remainingItems)
            {
                int startIndex = _index;
                _index += count;
                _bytesRead += count;
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
                    _bytesRead += remainingItems;
                    result.AddRange(GetBytes(startIndex, remainingItems));
                }
                result.AddRange(new byte[count - remainingItems]);
                return result.ToArray();
            }
        }

        private byte[] GetNextItemsBackward(int count)
        {
            int remainingItems = Math.Max(0, _index + 1);

            if (count <= remainingItems)
            {
                int startIndex = _index - count + 1;
                _index -= count;
                _bytesRead += count;
                return GetBytes(startIndex, count).Reverse().ToArray();
            }
            else
            {
                // return all remaining items, adding zero bytes for the missing items
                List<byte> result = new List<byte>();
                if (remainingItems > 0)
                {
                    int startIndex = _index - count + 1;
                    _index -= count;
                    _bytesRead += remainingItems;
                    result.AddRange(GetBytes(startIndex, remainingItems).Reverse().ToArray());
                }
                result.AddRange(new byte[count - remainingItems]);
                return result.ToArray();
            }
        }

        public byte[] TestGetNextItems(int count)
        {
            return GetNextItems(count);
        }
    }

    internal class IndexedReaderByteProvider : ByteStreamBufferedProvider
    {
        private readonly IndexedReader _reader;

        public IndexedReaderByteProvider(IndexedReader reader, int startIndex, int bufferLength, ExtractionDirection extractionDirection)
            : base(reader.Length, startIndex, bufferLength, extractionDirection)
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

        public StringByteProvider2(string input, int startIndex, int bufferLength, ExtractionDirection extractionDirection)
            : base(input.Length, startIndex, bufferLength, extractionDirection)
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
