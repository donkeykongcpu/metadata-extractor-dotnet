// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Tiff;
using MetadataExtractor.Formats.Xmp;

#if NET35
using DirectoryList = System.Collections.Generic.IList<MetadataExtractor.Directory>;
#else
using DirectoryList = System.Collections.Generic.IReadOnlyList<MetadataExtractor.Directory>;
#endif

namespace MetadataExtractor.Formats.Pdf
{
    /// <summary>
    /// Models object references found in the Cross-Reference (Xref) Table via <see cref="PdfReader.ProcessAtoms"/>.
    /// </summary>
    internal sealed class XrefEntry
    {
        /// <summary>
        /// Gets the sequential object number.
        /// </summary>
        public uint ObjectNumber { get; }

        /// <summary>
        /// Gets the 10-digit decimal number indicating the byte offset within the file.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// The 5-digit generation number. The maximum generation number is 65,535.
        /// </summary>
        public ushort Generation { get; }

        public XrefEntry(uint objectNumber, long offset, ushort generation)
        {
            ObjectNumber = objectNumber;
            Offset = offset;
            Generation = generation;
        }
    }

    /// <summary>Reads file passed in through SequentialReader and parses encountered data:</summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Basic EPS Comments</item>
    ///   <item>EXIF</item>
    ///   <item>Photoshop</item>
    ///   <item>IPTC</item>
    ///   <item>ICC Profile</item>
    ///   <item>XMP</item>
    /// </list>
    /// EPS comments are retrieved from EPS directory.  Photoshop, ICC Profile, and XMP processing
    /// is passed to their respective reader.
    /// <para />
    /// EPS Constraints (Source: https://www-cdf.fnal.gov/offline/PostScript/5001.PDF pg.18):
    /// <list type = "bullet" >
    ///   <item>Max line length is 255 characters</item>
    ///   <item>Lines end with a CR(0xD) or LF(0xA) character (or both, in practice)</item>
    ///   <item>':' separates keywords (considered part of the keyword)</item>
    ///   <item>Whitespace is either a space(0x20) or tab(0x9)</item>
    ///   <item>If there is more than one header, the 1st is truth</item>
    /// </list>
    /// </remarks>
    /// <author>Payton Garland</author>
    /// <author>Kevin Mott https://github.com/kwhopper</author>
    public sealed class PdfReader
    {
        private static byte[] PreambleBytes { get; } = Encoding.ASCII.GetBytes("%PDF-");

        private static char[] _whitespaceChars = new byte[] { 0x00, 0x09, 0x0A, 0x0C, 0x0D, 0x20 }.Select(b => (char)b).ToArray();

        private static char[] _tokenSeparatorChars = new byte[] { 0x00, 0x09, 0x0C, 0x20 }.Select(b => (char)b).ToArray();

        private int _previousTag;

        public DirectoryList Extract(Stream inputStream)
        {
            var directory = new PdfDirectory();
            var pdfDirectories = new List<Directory>() { directory };

            // %PDF-1.N signifies a PDF File Header, where N is a digit between 0 and 7

            int startingPosition = (int)inputStream.Position;

            var buffer = new byte[PreambleBytes.Length + 3];

            int read = inputStream.Read(buffer, 0, PreambleBytes.Length + 3);

            if (read < PreambleBytes.Length + 3 || !buffer.StartsWith(PreambleBytes))
            {
                directory.AddError("File type not supported.");
            }
            else
            {
                if (TryDecimalToInt(buffer[5]) >= 0 && buffer[6] == '.' && TryDecimalToInt(buffer[7]) >= 0)
                {
                    var version = Encoding.ASCII.GetString(buffer, 5, 3); // if version >= 1.4, /Version entry takes precedence

                    directory.Set(PdfDirectory.TagVersion, version);

                    inputStream.Position = startingPosition;
                    Extract(directory, pdfDirectories, new SequentialStreamReader(inputStream));
                }
                else
                {
                    directory.AddError("Missing PDF version in header.");
                }
            }

            return pdfDirectories;
        }

        /// <summary>
        /// Main method that parses all comments and then distributes data extraction among other methods that parse the
        /// rest of file and store encountered data in metadata(if there exists an entry in EpsDirectory
        /// for the found data).  Reads until a begin data/binary comment is found or reader's estimated
        /// available data has run out (or AI09 End Private Data).  Will extract data from normal EPS comments, Photoshop, ICC, and XMP.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="directories">list to add directory to and extracted data</param>
        /// <param name="reader"></param>
        private void Extract(PdfDirectory directory, List<Directory> directories, SequentialReader reader)
        {
            var line = new StringBuilder();

            while (true)
            {
                line.Length = 0;

                // Read the next line, excluding any trailing newline character
                // Note that for Windows-style line endings ("\r\n") the outer loop will be run a second time with an empty
                // string, which is fine.
                while (true)
                {
                    char c = (char)reader.GetByte();
                    if (c == '\r' || c == '\n')
                        break;
                    line.Append(c);
                }

                // Stop when we hit a line that is not a comment
                if (line.Length != 0 && line[0] != '%')
                    break;

                string name;

                // ':' signifies there is an associated keyword (should be put in directory)
                // otherwise, the name could be a marker
                int colonIndex = line.IndexOf(':');
                if (colonIndex != -1)
                {
                    name = line.ToString(0, colonIndex).Trim();
                    var value = line.ToString(colonIndex + 1, line.Length - (colonIndex + 1)).Trim();
                    AddToDirectory(directory, name, value);
                }
                else
                    name = line.ToString().Trim();

                // Some comments will both have a value and signify a new block to follow
                if (name.Equals("%BeginPhotoshop"))
                    ExtractPhotoshopData(directories, reader);
                else if (name.Equals("%%BeginICCProfile"))
                    ExtractIccData(directories, reader);
                else if (name.Equals("%begin_xml_packet"))
                    ExtractXmpData(directories, reader);
            }
        }

        /// <summary>
        /// Default case that adds comment with keyword to directory
        /// </summary>
        /// <param name="directory">EpsDirectory to add extracted data to</param>
        /// <param name="name">String that holds name of current comment</param>
        /// <param name="value">String that holds value of current comment</param>
        private void AddToDirectory(PdfDirectory directory, string name, string value)
        {
            if (!PdfDirectory.TagIntegerMap.ContainsKey(name))
                return;

            var tag = PdfDirectory.TagIntegerMap[name];

            switch (tag)
            {
                case PdfDirectory.TagImageData:
                    ExtractImageData(directory, value);
                    break;
                case PdfDirectory.TagContinueLine:
                    directory.Set(_previousTag, directory.GetString(_previousTag) + " " + value);
                    break;
                default:
                    if (PdfDirectory.TagNameMap.ContainsKey(tag) && !directory.ContainsTag(tag))
                    {
                        directory.Set(tag, value);
                        _previousTag = tag;
                    }
                    else
                    {
                        // Set previous tag to an Integer that doesn't exist in EpsDirectory
                        _previousTag = 0;
                    }
                    break;
            }
            _previousTag = tag;
        }

        /// <summary>
        /// Parses '%ImageData' comment which holds several values including width in px,
        /// height in px and color type.
        /// </summary>
        private static void ExtractImageData(PdfDirectory directory, string imageData)
        {
            // %ImageData: 1000 1000 8 3 1 1000 7 "beginimage"
            directory.Set(PdfDirectory.TagImageData, imageData.Trim());

            var imageDataParts = imageData.Split(' ');

            int width = int.Parse(imageDataParts[0]);
            int height = int.Parse(imageDataParts[1]);
            int colorType = int.Parse(imageDataParts[3]);

            // Only add values that are not already present
            if (!directory.ContainsTag(PdfDirectory.TagImageWidth))
                directory.Set(PdfDirectory.TagImageWidth, width);
            if (!directory.ContainsTag(PdfDirectory.TagImageHeight))
                directory.Set(PdfDirectory.TagImageHeight, height);
            if (!directory.ContainsTag(PdfDirectory.TagColorType))
                directory.Set(PdfDirectory.TagColorType, colorType);

            if (!directory.ContainsTag(PdfDirectory.TagRamSize))
            {
                int bytesPerPixel = colorType switch
                {
                    1 => 1, // grayscale
                    2 => 3, // Lab
                    3 => 3, // RGB
                    4 => 3, // CMYK
                    _ => 0
                };

                if (bytesPerPixel != 0)
                    directory.Set(PdfDirectory.TagRamSize, bytesPerPixel * width * height);
            }
        }

        /// <summary>
        /// Decodes a commented hex section, and uses <see cref="PhotoshopReader"/> to decode the resulting data.
        /// </summary>
        private static void ExtractPhotoshopData(List<Directory> directories, SequentialReader reader)
        {
            var buffer = DecodeHexCommentBlock(reader);

            if (buffer is not null)
                directories.AddRange(new PhotoshopReader().Extract(new SequentialByteArrayReader(buffer), buffer.Length));
        }

        /// <summary>
        /// Decodes a commented hex section, and uses <see cref="IccReader"/> to decode the resulting data.
        /// </summary>
        private static void ExtractIccData(List<Directory> directories, SequentialReader reader)
        {
            var buffer = DecodeHexCommentBlock(reader);

            if (buffer is not null)
                directories.Add(new IccReader().Extract(new ByteArrayReader(buffer)));
        }

        /// <summary>
        /// Extracts an XMP xpacket, and uses <see cref="XmpReader"/> to decode the resulting data.
        /// </summary>
        private static void ExtractXmpData(List<Directory> directories, SequentialReader reader)
        {
            byte[] xmp = ReadUntil(reader, Encoding.UTF8.GetBytes("<?xpacket end=\"w\"?>"));
            directories.Add(new XmpReader().Extract(xmp));
        }

        private static string[] ExtractIndirectObject(IndexedReader reader, int index, uint cmpObjectNumber, ushort cmpGeneration)
        {
            // extract the object found at index, using at least 4 tokens: objectNumber generation "obj" ... "endobj"

            int nextIndex = index;

            var tokens = new List<string>();

            uint objectNumber = reader.GetUInt32Token(ref nextIndex);

            if (objectNumber != cmpObjectNumber)
            {
                throw new Exception("Unexpected object number");
            }

            ushort generation = reader.GetUInt16Token(ref nextIndex);

            if (generation != cmpGeneration)
            {
                throw new Exception("Unexpected object generation");
            }

            string marker = reader.GetToken(ref nextIndex);

            if (marker != "obj")
            {
                throw new Exception("Object marker not found");
            }

            string token;

            while ((token = reader.GetToken(ref nextIndex)) != "endobj")
            {
                tokens.Add(token);
            }

            return tokens.ToArray();
        }

        private XrefEntry[] ExtractXrefTable(IndexedReader reader, int xrefOffset, int size)
        {
            var result = new XrefEntry[size];

            // starting at xrefOffset, we expect the "xref" marker, followed by two integers

            int nextIndex = xrefOffset;

            var marker = reader.GetToken(ref nextIndex);

            if (marker != "xref")
            {
                // TODO throw or report error?
            }

            // there can be multiple subsections, each starting with two integers
            uint firstObjectNumber, numberOfObjects;

            while (reader.TryGetUInt32Token(ref nextIndex, out firstObjectNumber) && reader.TryGetUInt32Token(ref nextIndex, out numberOfObjects))
            {
                for (int i = 0; i < numberOfObjects; i++)
                {
                    // each entry is supposed to be exactly 20 bytes long (according to the spec),
                    // but some sample files have 19-byte entries (omitting a byte for the end-of-line marker)
                    var objectNumber = (uint)(firstObjectNumber + i); // zero-indexed
                    long offset = long.Parse(reader.GetToken(ref nextIndex));
                    ushort generation = ushort.Parse(reader.GetToken(ref nextIndex));
                    var usageMarker = reader.GetToken(ref nextIndex);
                    if (usageMarker == "f") continue; // entry is "free": don't track it
                    result[objectNumber] = new XrefEntry(objectNumber, offset, generation);
                }
            }

            return result;
        }

        private static object? ParseObject(IndexedReader reader, XrefEntry[] xrefTable, string[] tokens)
        {
            if (tokens is null || tokens.Length == 0)
            {
                return null;
            }

            // first check if we have an indirect reference to an object
            // if so, there should be exactly 3 tokens: objectNumber generation "R" (the keyword "R" indicates an indirect reference)
            if (tokens.Length == 3 && tokens[2] == "R")
            {
                uint objectNumber = uint.Parse(tokens[0]);
                ushort generation = ushort.Parse(tokens[1]);

                XrefEntry? reference = xrefTable[objectNumber];

                if (reference is not null && reference.Generation == generation)
                {
                    var remainingTokens = ExtractIndirectObject(reader, (int)reference.Offset, objectNumber, generation);
                    return ParseObject(reader, xrefTable, remainingTokens);
                }
                else
                {
                    return null;
                }
            }
            else if (tokens.Length >= 4 && tokens[2] == "obj" && tokens[tokens.Length - 1] == "endobj")
            {
                // alternatively, the object can be specified inline, using at least 4 tokens: objectNumber generation "obj" ... "endobj"
                var remainingTokens = tokens.Skip(3).Take(tokens.Length - 4).ToArray();
                return ParseObject(reader, xrefTable, remainingTokens);
            }

            var firstToken = tokens.First();

            // check single-token values first (Null, Boolean, Numeric, Name)

            if (tokens.Length == 1)
            {
                switch (firstToken)
                {
                    case "null": return null;
                    case "true": return true;
                    case "false": return false;
                }

                if (int.TryParse(firstToken, out var intValue))
                {
                    return intValue;
                }

                if (decimal.TryParse(firstToken, out var decimalValue))
                {
                    return decimalValue;
                }

                if (firstToken.StartsWith("/"))
                {
                    return ParseName(firstToken);
                }
            }

            // then check Array, Dictionary, Stream objects

            if (firstToken.StartsWith("<<"))
            {
                return ParseDictionary(reader, xrefTable, tokens);
            }
            else if (firstToken.StartsWith("["))
            {
                return ParseArray(reader, xrefTable, tokens);
            }
            else if (firstToken.StartsWith("("))
            {
                return ParseLiteralString(tokens);
            }
            else if (firstToken.StartsWith("<"))
            {
                return ParseHexadecimalString(tokens);
            }

            // TODO add ParseStream

            return "UNKNOWN: " + string.Join(" ", tokens.ToArray()); // unknown value
        }

        private static string ParseName(string token)
        {
            return token; // TODO finish this
        }

        private static string ParseLiteralString(IEnumerable<string> tokens)
        {
            return "LITERAL STRING: " + string.Join(" ", tokens.ToArray());
        }

        private static string ParseHexadecimalString(IEnumerable<string> tokens)
        {
            return "HEX STRING: " + string.Join(" ", tokens.ToArray());
        }

        private static IEnumerable<object?> ParseArray(IndexedReader reader, XrefEntry[] xrefTable, IEnumerable<string> tokens)
        {
            return tokens.Select(token => ParseObject(reader, xrefTable, new string[] { token })); // TODO not sure if these are necessary
        }

        private static Dictionary<string, object?> ParseDictionary(IndexedReader reader, XrefEntry[] xrefTable, IEnumerable<string> tokens)
        {
            var tokenDictionary = ParseTokenDictionary(tokens);

            return tokenDictionary.ToDictionary(item => ParseName(item.Key), item => ParseObject(reader, xrefTable, item.Value));
        }

        private static Dictionary<string, string[]> ParseTokenDictionary(IEnumerable<string> tokens)
        {
            // we need to extract the /Size and /Prev integer values from the trailer,
            // in order to build the Cross-Reference Table

            // because the Cross-Reference Table is not yet built,
            // we cannot extract indirect references yet (e.g. /Root, /Info, etc.)

            var result = new Dictionary<string, string[]>();

            string? name = null;

            var value = new List<string>();

            var nameAsValueAllowed = false;

            foreach (var token in tokens)
            {
                if (token == "<<")
                {
                    continue;
                }
                else if (token == ">>")
                {
                    break;
                }
                else if (token.StartsWith("/"))
                {
                    if (nameAsValueAllowed)
                    {
                        // this name token is being used as a value
                        value.Add(token);
                        nameAsValueAllowed = false;
                    }
                    else
                    {
                        // this name token is being used as a key
                        if (name is not null && value.Count > 0)
                        {
                            result[name] = value.ToArray();
                        }
                        value.Clear();
                        name = token;
                        nameAsValueAllowed = true;
                    }
                }
                else
                {
                    value.Add(token);
                    nameAsValueAllowed = false;
                }
            }

            if (name is not null && value.Count > 0)
            {
                result[name] = value.ToArray();
            }

            return result;
        }

        /// <summary>
        /// Reads all bytes until the given sentinel is observed.
        /// The sentinel will be included in the returned bytes.
        /// </summary>
        private static byte[] ReadUntil(SequentialReader reader, byte[] sentinel)
        {
            var bytes = new MemoryStream();

            int length = sentinel.Length;
            int depth = 0;

            while (depth != length)
            {
                byte b = reader.GetByte();
                if (b == sentinel[depth])
                    depth++;
                else
                    depth = 0;
                bytes.WriteByte(b);
            }

            return bytes.ToArray();
        }

        private static List<List<string>> ReadFromEndUntil(IndexedReader reader, string sentinel)
        {
            var line = new List<string>();
            var token = new StringBuilder();
            int index = (int)reader.Length - 1;

            var result = new List<List<string>> { line };

            while (index >= 0)
            {
                var nextByte = reader.GetByte(index);

                if (nextByte == '\r' || nextByte == '\n')
                {
                    // add current token to current line and start new line and new token
                    if (token.Length > 0)
                    {
                        line.Add(new string(token.ToString().ToCharArray().Reverse().ToArray()));
                    }
                    line.Reverse();

                    if (line.Count > 0 && line[0] == sentinel)
                    {
                        break;
                    }

                    line = new List<string>();
                    result.Add(line);
                    token = new StringBuilder();
                }
                else if (_tokenSeparatorChars.Contains((char)nextByte))
                {
                    // add current token to current line and start new token
                    if (token.Length > 0)
                    {
                        line.Add(new string(token.ToString().ToCharArray().Reverse().ToArray()));
                    }

                    token = new StringBuilder();
                }
                else
                {
                    // add to current token
                    token.Append((char)nextByte);
                }

                index--;
            }

            result.Reverse();

            return result.Where(line => line.Count > 0).ToList(); // excluding lines with no tokens
        }

        /**
         * EPS files can contain hexadecimal-encoded ASCII blocks, each prefixed with <c>"% "</c>.
         * This method reads such a block and returns a byte[] of the decoded contents.
         * Reading stops at the first invalid line, which is discarded (it's a terminator anyway).
         * <p/>
         * For example:
         * <pre><code>
         * %BeginPhotoshop: 9564
         * % 3842494D040400000000005D1C015A00031B25471C0200000200041C02780004
         * % 6E756C6C1C027A00046E756C6C1C025000046E756C6C1C023700083230313630
         * % 3331311C023C000B3131343335362B303030301C023E00083230313630333131
         * % 48000000010000003842494D03FD0000000000080101000000000000
         * %EndPhotoshop
         * </code></pre>
         * When calling this method, the reader must be positioned at the start of the first line containing
         * hex data, not at the introductory line.
         *
         * @return The decoded bytes, or <code>null</code> if decoding failed.
         */
        /// <remarks>
        /// EPS files can contain hexadecimal-encoded ASCII blocks, each prefixed with "% ".
        /// This method reads such a block and returns a byte[] of the decoded contents.
        /// Reading stops at the first invalid line, which is discarded(it's a terminator anyway).
        /// <para />
        /// For example:
        /// <para />
        /// %BeginPhotoshop: 9564
        /// % 3842494D040400000000005D1C015A00031B25471C0200000200041C02780004
        /// % 6E756C6C1C027A00046E756C6C1C025000046E756C6C1C023700083230313630
        /// % 3331311C023C000B3131343335362B303030301C023E00083230313630333131
        /// % 48000000010000003842494D03FD0000000000080101000000000000
        /// %EndPhotoshop
        /// <para />
        /// When calling this method, the reader must be positioned at the start of the first line containing
        /// hex data, not at the introductory line.
        /// </remarks>
        /// <returns>The decoded bytes, or null if decoding failed.</returns>
        private static byte[]? DecodeHexCommentBlock(SequentialReader reader)
        {
            var bytes = new MemoryStream();

            // Use a state machine to efficiently parse data in a single traversal

            const int AwaitingPercent = 0;
            const int AwaitingSpace = 1;
            const int AwaitingHex1 = 2;
            const int AwaitingHex2 = 3;

            int state = AwaitingPercent;

            int carry = 0;
            bool done = false;

            byte b = 0;
            while (!done)
            {
                b = reader.GetByte();

                switch (state)
                {
                    case AwaitingPercent:
                    {
                        switch (b)
                        {
                            case (byte)'\r':
                            case (byte)'\n':
                            case (byte)' ':
                                // skip newline chars and spaces
                                break;
                            case (byte)'%':
                                state = AwaitingSpace;
                                break;
                            default:
                                return null;
                        }
                        break;
                    }
                    case AwaitingSpace:
                    {
                        switch (b)
                        {
                            case (byte)' ':
                                state = AwaitingHex1;
                                break;
                            default:
                                done = true;
                                break;
                        }
                        break;
                    }
                    case AwaitingHex1:
                    {
                        int i = TryHexToInt(b);
                        if (i != -1)
                        {
                            carry = i * 16;
                            state = AwaitingHex2;
                        }
                        else if (b == '\r' || b == '\n')
                        {
                            state = AwaitingPercent;
                        }
                        else
                        {
                            return null;
                        }
                        break;
                    }
                    case AwaitingHex2:
                    {
                        int i = TryHexToInt(b);
                        if (i == -1)
                            return null;
                        bytes.WriteByte((byte)(carry + i));
                        state = AwaitingHex1;
                        break;
                    }
                }
            }

            // skip through the remainder of the last line
            while (b != '\n')
                b = reader.GetByte();

            return bytes.ToArray();
        }

        /// <summary>
        /// Treats a byte as an ASCII character, and returns its numerical value in decimal.
        /// If conversion is not possible, returns -1.
        /// </summary>
        private static int TryDecimalToInt(byte b)
        {
            if (b >= '0' && b <= '9')
                return b - '0';
            return -1;
        }

        /// <summary>
        /// Treats a byte as an ASCII character, and returns its numerical value in hexadecimal.
        /// If conversion is not possible, returns -1.
        /// </summary>
        private static int TryHexToInt(byte b)
        {
            if (b >= '0' && b <= '9')
                return b - '0';
            if (b >= 'A' && b <= 'F')
                return b - 'A' + 10;
            if (b >= 'a' && b <= 'f')
                return b - 'a' + 10;
            return -1;
        }
    }
}
