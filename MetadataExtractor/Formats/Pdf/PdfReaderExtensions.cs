// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace MetadataExtractor.Formats.Pdf
{
    /// <summary>
    /// Extension methods for reading Pdf specific encodings from an <see cref="IndexedReader"/>.
    /// </summary>
    public static class PdfReaderExtensions
    {
        public static string GetToken(this IndexedReader reader, ref int nextIndex)
        {
            var sb = new StringBuilder();
            bool awaitingNonWhitespace = true;
            while (true)
            {
                var nextByte = reader.GetByte(nextIndex++);
                if (PdfReader.WhitespaceChars.Contains((char)nextByte))
                {
                    if (awaitingNonWhitespace)
                    {
                        continue; // wait until first non-whitespace character is found
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    awaitingNonWhitespace = false; // first non-whitespace character found
                }
                sb.Append((char)nextByte);
            }
            return sb.ToString();
        }

        public static bool TryGetToken(this IndexedReader reader, ref int nextIndex, out string result)
        {
            try
            {
                result = reader.GetToken(ref nextIndex);
                return true;
            }
            catch (Exception)
            {
                result = string.Empty;
                return false;
            }
        }

        public static ushort GetUInt16Token(this IndexedReader reader, ref int index)
        {
            return ushort.Parse(reader.GetToken(ref index));
        }

        public static uint GetUInt32Token(this IndexedReader reader, ref int index)
        {
            return uint.Parse(reader.GetToken(ref index));
        }

        public static bool TryGetUInt32Token(this IndexedReader reader, ref int index, out uint result)
        {
            try
            {
                result = uint.Parse(reader.GetToken(ref index));
                return true;
            }
            catch (Exception)
            {
                result = 0;
                return false;
            }
        }

        public static long GetInt64Token(this IndexedReader reader, ref int index)
        {
            return long.Parse(reader.GetToken(ref index));
        }
    }
}
