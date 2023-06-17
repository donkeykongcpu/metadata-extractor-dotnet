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

    internal interface PdfObject
    {
        string Type { get; }

        object? GetValue();

        void Add(object? value);
    }

    internal class PdfRoot : PdfObject
    {
        private object? _value;

        private bool _valueWasSet;

        public string Type => "root";

        public PdfRoot()
        {
            _valueWasSet = false;
        }

        public object? GetValue()
        {
            if (!_valueWasSet)
            {
                throw new Exception("Value was not set");
            }
            return _value;
        }

        public void Add(object? value)
        {
            if (_valueWasSet)
            {
                throw new Exception("Value already set");
            }
            _value = value;
            _valueWasSet = true;
        }
    }

    internal class PdfArray : PdfObject
    {
        private readonly List<object?> _array;

        public string Type => "array";

        public PdfArray()
        {
            _array = new List<object?>();
        }

        public object? GetValue()
        {
            return _array;
        }

        public void Add(object? value)
        {
            _array.Add(value);
        }
    }

    internal class PdfDictionary : PdfObject
    {
        private readonly Dictionary<string, object?> _dictionary;
        private string? _currentKey = null;

        public string Type => "dictionary";

        public PdfDictionary()
        {
            _dictionary = new Dictionary<string, object?>();
        }

        public object? GetValue()
        {
            return _dictionary;
        }

        public void Add(object? value)
        {
            if (value is NameToken)
            {
                if (_currentKey is not null)
                {
                    _dictionary.Add(_currentKey, value);
                    _currentKey = null;
                }
                else
                {
                    _currentKey = Encoding.ASCII.GetString((value as NameToken)!.Value); // TODO: dictionary keeps are probably ASCII
                }
            }
            else
            {
                if (_currentKey is null)
                {
                    return;
                }
                _dictionary.Add(_currentKey, value);
                _currentKey = null;
            }
        }
    }

    internal sealed class ParseContext
    {
        private readonly Stack<PdfObject> _stack;

        private string ContextType
        {
            get
            {
                return _stack.Peek().Type;
            }
        }

        public ParseContext()
        {
            _stack = new Stack<PdfObject>();
            _stack.Push(new PdfRoot());
        }

        public void Add(object? value)
        {
            _stack.Peek().Add(value);
        }

        public void StartContext(string type)
        {
            PdfObject pdfObject;
            switch (type)
            {
                case "root": pdfObject = new PdfRoot(); break;
                case "array": pdfObject = new PdfArray(); break;
                case "dictionary": pdfObject = new PdfDictionary(); break;
                default: throw new Exception("Invalid type");
            }
            Add(pdfObject);
            _stack.Push(pdfObject);
        }

        public void EndContext(string type)
        {
            if (ContextType != type)
            {
                throw new Exception("Context type mismatch");
            }
            _stack.Pop();
            if (_stack.Count < 1)
            {
                throw new Exception("Context underflow");
            }
        }

        public object? GetValue()
        {
            if (_stack.Count != 1)
            {
                throw new Exception("Invalid context count");
            }
            return _stack.Peek().GetValue();
        }
    }

    internal abstract class ByteProvider
    {
        private int _index;

        protected abstract int Length { get; }

        protected ByteProvider(int index)
        {
            _index = index;
        }

        public bool HasNextByte()
        {
            return (_index < Length);
        }

        public byte GetNextByte()
        {
            if (_index >= Length)
            {
                return 0;
            }
            byte result = DoGetByte(_index);
            _index++;
            return result;
        }

        public byte PeekByte(int delta)
        {
            if (_index + delta < 0)
            {
                return 0;
            }
            if (_index + delta >= Length)
            {
                return 0;
            }
            return DoGetByte(_index + delta);
        }

        public bool MatchDelimiter(string delimiter)
        {
            byte[] needle = Encoding.ASCII.GetBytes(delimiter.ToCharArray());
            for (int i = 0; i < needle.Length; i++)
            {
                if (needle[i] != PeekByte(i))
                {
                    return false;
                }
            }
            return true;
        }

        public bool MatchToken(string token)
        {
            byte[] needle = Encoding.ASCII.GetBytes(token.ToCharArray());
            if (!PdfReader.WhitespaceChars.Contains(PeekByte(needle.Length)))
            {
                return false;
            }
            for (int i = 0; i < needle.Length; i++)
            {
                if (needle[i] != PeekByte(i))
                {
                    return false;
                }
            }
            return true;
        }

        public bool TryMatchNumericToken([NotNullWhen(true)] out Token? token)
        {
            StringBuilder test = new StringBuilder();
            bool isDecimal = false;
            for (int i = 0; ; i++)
            {
                byte nextByte = PeekByte(i);
                if ((nextByte >= (byte)'0' && nextByte <= (byte)'9') || nextByte == (byte)'-' || nextByte == (byte)'+')
                {
                    test.Append((char)nextByte);
                }
                else if (nextByte == (byte)'.')
                {
                    test.Append((char)nextByte);
                    isDecimal = true;
                }
                else
                {
                    string strValue = test.ToString();
                    if (isDecimal && decimal.TryParse(strValue, out decimal decimalValue))
                    {
                        token = new NumericRealToken(decimalValue, strValue.ToCharArray().Select(x => (byte)x).ToArray());
                        return true;
                    }
                    else if (int.TryParse(strValue, out int intValue))
                    {
                        token = new NumericIntegerToken(intValue, strValue.ToCharArray().Select(x => (byte)x).ToArray());
                        return true;
                    }
                    else
                    {
                        token = null;
                        return false;
                    }
                }
            }
        }

        public void Consume(int count)
        {
            if (_index + count >= Length)
            {
                _index = Length;
            }
            else
            {
                _index += count;
            }
        }

        public bool TryPeekOctalDigit(int delta, out byte result)
        {
            byte test = PeekByte(delta);
            if (test >= (byte)'0' && test <= (byte)'7')
            {
                result = (byte)(test - (byte)'0');
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }

        public bool TryPeekHexadecimalDigit(int delta, out byte result)
        {
            byte test = PeekByte(delta);
            if (test >= (byte)'0' && test <= (byte)'9')
            {
                result = (byte)(test - (byte)'0');
                return true;
            }
            else if (test >= (byte)'A' && test <= (byte)'F')
            {
                result = (byte)(test - (byte)'A' + 10);
                return true;
            }
            else if (test >= (byte)'a' && test <= (byte)'f')
            {
                result = (byte)(test - (byte)'a' + 10);
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }

        protected abstract byte DoGetByte(int index);
    }

    internal class StringByteProvider : ByteProvider
    {
        private readonly byte[] _input;

        protected override int Length => _input.Length;

        public StringByteProvider(string input)
            : base(index: 0)
        {
            _input = input.ToCharArray().Select(x => (byte)x).ToArray();
        }

        protected override byte DoGetByte(int index)
        {
            return _input[index];
        }

    }

    internal class StreamByteProvider : ByteProvider
    {
        private readonly IndexedReader _reader;

        protected override int Length => (int)_reader.Length;

        public StreamByteProvider(IndexedReader reader, int index)
            : base(index: index)
        {
            _reader = reader;
        }

        protected override byte DoGetByte(int index)
        {
            return _reader.GetByte(index);
        }

    }

    public abstract class Token : IEquatable<Token>
    {
        public abstract string Type { get; }

        public byte[] Value { get; }

        protected Token(byte[] value)
        {
            Value = value;
        }

        protected Token(string value)
        {
            // value should only contain 1-byte characters

            Value = value.ToCharArray().Select(x => (byte)x).ToArray();
        }

        public bool Equals(Token other)
        {
            if (other is null) return false;
            if (other.Type != Type) return false;
            return Value.EqualTo(other.Value);
        }
    }

    internal class DummyToken : Token
    {
        public override string Type => "dummy";
        public DummyToken() : base("dummy") { }
    }

    internal class IndirectReferenceToken : Token
    {
        public uint ObjectNumber { get; }

        public ushort Generation { get; }

        public override string Type => "indirect-reference";

        public IndirectReferenceToken(int objectNumber, int generation)
            : base(new byte[] { })
        {
            ObjectNumber = (uint)objectNumber;

            Generation = (ushort)generation;
        }

        public override string ToString()
        {
            return "{@" + ObjectNumber + " " + Generation + "}";
        }
    }

    internal class IndirectReferenceMarkerToken : Token
    {
        public override string Type => "R";
        public IndirectReferenceMarkerToken() : base("R") { }
    }

    internal class IndirectObjectBeginToken : Token
    {
        public override string Type => "obj";
        public IndirectObjectBeginToken() : base("obj") { }
    }

    internal class IndirectObjectEndToken : Token
    {
        public override string Type => "endobj";
        public IndirectObjectEndToken() : base("endobj") { }
    }

    internal class StreamBeginToken : Token
    {
        public override string Type => "stream";
        public StreamBeginToken() : base("stream") { }
    }

    internal class StreamEndToken : Token
    {
        public override string Type => "endstream";
        public StreamEndToken() : base("endstream") { }
    }

    internal class NullToken : Token
    {
        public override string Type => "null";
        public NullToken() : base("null") { }
        public override string ToString()
        {
            return "null";
        }
    }

    internal class BooleanTrueToken : Token
    {
        public override string Type => "true";
        public BooleanTrueToken() : base("true") { }
        public override string ToString()
        {
            return "true";
        }
    }

    internal class BooleanFalseToken : Token
    {
        public override string Type => "false";
        public BooleanFalseToken() : base("false") { }
        public override string ToString()
        {
            return "false";
        }
    }

    public class NumericIntegerToken : Token
    {
        public int IntegerValue { get; }

        public override string Type => "numeric-integer";

        public NumericIntegerToken(int value, byte[] rawValue)
            : base(rawValue)
        {
            IntegerValue = value;
        }
        public override string ToString()
        {
            return IntegerValue.ToString();
        }
    }

    public class NumericRealToken : Token
    {
        public decimal RealValue { get; }

        public override string Type => "numeric-real";

        public NumericRealToken(decimal value, byte[] rawValue)
            : base(rawValue)
        {
            RealValue = value;
        }
        public override string ToString()
        {
            return RealValue.ToString();
        }
    }

    internal class ArrayBeginToken : Token
    {
        public override string Type => "array-begin";
        public ArrayBeginToken() : base("[") { }
    }

    internal class ArrayEndToken : Token
    {
        public override string Type => "array-end";
        public ArrayEndToken() : base("]") { }
    }

    internal class DictionaryBeginToken : Token
    {
        public override string Type => "dictionary-begin";
        public DictionaryBeginToken() : base("<<") { }
    }

    internal class DictionaryEndToken : Token
    {
        public override string Type => "dictionary-end";
        public DictionaryEndToken() : base(">>") { }
    }

    public class StringToken : Token
    {
        public override string Type => "string";

        public StringToken(byte[] value)
            : base(value)
        {

        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(Value); // TODO: encoding is context-specific
        }

        public string ToASCIIString()
        {
            return Encoding.ASCII.GetString(Value);
        }

        public string ToUTF8String()
        {
            return Encoding.UTF8.GetString(Value);
        }
    }

    public class NameToken : Token
    {
        public override string Type => "name";

        public NameToken(byte[] value)
            : base(value)
        {
            // the value does not include the leading slash (/)
        }

        public override string ToString()
        {
            // names are not usually intended to be printed,
            // but when they are, their encoding is supposed to be UTF8
            return Encoding.UTF8.GetString(Value);
        }
    }

    public class CommentToken : Token
    {
        public override string Type => "comment";

        public CommentToken(byte[] value)
            : base(value)
        {
            // the value does not include the leading slash (/)
        }
    }

    internal abstract class TokeniseContext
    {
        private List<byte> _token;

        private readonly ByteProvider _provider;

        protected ByteProvider Provider
        {
            get => _provider;
        }

        public string Type { get; private set; }

        protected int Counter { get; private set; }

        protected TokeniseContext(string type, ByteProvider provider)
        {
            Type = type;
            Counter = 0;
            _token = new List<byte>();
            _provider = provider;
        }

        public abstract IEnumerable<Token> Consume();

        protected void Append(byte b)
        {
            _token.Add(b);
        }

        protected void Append(char c)
        {
            byte test = (byte)c;
            if (c != test)
            {
                throw new Exception("Invalid byte");
            }
            _token.Add(test);
        }

        protected byte[] GetToken()
        {
            byte[] result = _token.ToArray();
            _token.Clear();
            return result;
        }

        protected void IncrementCounter()
        {
            Counter++;
        }

        protected void DecrementCounter()
        {
            Counter--;
            if (Counter < 0)
            {
                throw new Exception("Counter cannot be negative");
            }
        }
    }

    internal class RootTokeniseContext : TokeniseContext
    {
        public RootTokeniseContext(ByteProvider provider)
          : base("root", provider)
        {

        }

        public override IEnumerable<Token> Consume()
        {
            throw new Exception("Cannot consume tokens at the root level");
        }
    }

    internal class LiteralStringTokeniseContext : TokeniseContext
    {
        public LiteralStringTokeniseContext(ByteProvider provider)
            : base("literal-string", provider)
        {

        }

        public override IEnumerable<Token> Consume()
        {
            // keep all characters within string

            IncrementCounter();

            while (true)
            {
                if (!Provider.HasNextByte())
                {
                    throw new Exception("Unexpected end of input");
                }

                byte nextByte = Provider.GetNextByte();

                if (nextByte == (byte)'(')
                {
                    IncrementCounter();
                    Append('(');
                }
                else if (nextByte == (byte)')')
                {
                    DecrementCounter();
                    if (Counter == 0)
                    {
                        yield return new StringToken(GetToken());
                        yield break; // done with this literal string
                    }
                    else
                    {
                        Append(')');
                    }
                }
                else if (nextByte == (byte)'\r' && Provider.PeekByte(0) == (byte)'\n')
                {
                    Append('\n'); // CRLF => LF
                    Provider.Consume(1);
                }
                else if (nextByte == (byte)'\r')
                {
                    Append('\n'); // CR => LF
                    Provider.Consume(0); // already consumed
                }
                else if (nextByte == (byte)'\\')
                {
                    // escape sequences
                    byte peekByte = Provider.PeekByte(0);
                    if (peekByte == (byte)'\r' && Provider.PeekByte(1) == (byte)'\n')
                    {
                        // \CRLF => ignored
                        Provider.Consume(2);
                    }
                    else if (Provider.TryPeekOctalDigit(0, out byte digit1))
                    {
                        Provider.Consume(1);

                        // octal character codes
                        // can consist of one, two or three digits
                        // high-order overflow shall be ignored
                        List<byte> digits = new List<byte> { digit1 };

                        if (Provider.TryPeekOctalDigit(0, out byte digit2))
                        {
                            digits.Add(digit2);
                            Provider.Consume(1);
                        }

                        if (Provider.TryPeekOctalDigit(0, out byte digit3))
                        {
                            digits.Add(digit3);
                            Provider.Consume(1);
                        }

                        int value = 0;
                        for (int i = 0; i < digits.Count; i++)
                        {
                            value += digits[digits.Count - 1 - i] * (int)Math.Pow(8, i);
                        }
                        byte coerced = (byte)value;
                        Append(coerced);
                    }
                    else
                    {
                        switch (peekByte)
                        {
                            case (byte)'n': Append('\n'); Provider.Consume(1); break;
                            case (byte)'r': Append('\r'); Provider.Consume(1); break;
                            case (byte)'t': Append('\t'); Provider.Consume(1); break;
                            case (byte)'b': Append('\b'); Provider.Consume(1); break;
                            case (byte)'f': Append('\f'); Provider.Consume(1); break;
                            case (byte)'(': Append('('); Provider.Consume(1); break;
                            case (byte)')': Append(')'); Provider.Consume(1); break;
                            case (byte)'\\': Append('\\'); Provider.Consume(1); break;
                            case (byte)'\n': Provider.Consume(1); break; // \LF => ignored
                            case (byte)'\r': Provider.Consume(1); break; // \CR => ignored
                            default: break; // ignore \ and process next byte
                        }
                    }
                }
                else
                {
                    Append(nextByte);
                }
            }
        }
    }

    internal class HexadecimalStringTokeniseContext : TokeniseContext
    {
        public HexadecimalStringTokeniseContext(ByteProvider provider)
            : base("hex-string", provider)
        {

        }

        public override IEnumerable<Token> Consume()
        {
            while (true)
            {
                if (!Provider.HasNextByte())
                {
                    throw new Exception("Unexpected end of input");
                }

                if (Provider.PeekByte(0) == (byte)'>')
                {
                    Provider.Consume(1);
                    byte[] token = GetToken();
                    if (token.Length % 2 != 0)
                    {
                        token = token.Concat(new byte[] { 0 }).ToArray(); // odd number of digits => append 0
                    }
                    List<byte> result = new List<byte>();
                    for (int i = 0; i < token.Count(); i += 2)
                    {
                        byte value = (byte)(token[i] * 16 + token[i + 1]);
                        result.Add(value);
                    }
                    yield return new StringToken(result.ToArray());
                    yield break; // done with this hexadecimal string
                }
                else if (Provider.TryPeekHexadecimalDigit(0, out byte result))
                {
                    Provider.Consume(1);
                    Append(result);
                }
                else if (PdfReader.WhitespaceChars.Contains(Provider.PeekByte(0)))
                {
                    Provider.Consume(1); // ignore whitespace within
                }
                else
                {
                    throw new Exception("Unexpected byte in hexadecimal string");
                }
            }
        }
    }

    internal class NameTokeniseContext : TokeniseContext
    {
        public NameTokeniseContext(ByteProvider provider)
            : base("name", provider)
        {

        }

        public override IEnumerable<Token> Consume()
        {
            while (true)
            {
                if (!Provider.HasNextByte() || PdfReader.WhitespaceChars.Contains(Provider.PeekByte(0)))
                {
                    Provider.Consume(1);
                    yield return new NameToken(GetToken()); // does not include the leading slash (/)
                    yield break; // done with this name
                }
                else if (Provider.PeekByte(0) == (byte)'#')
                {
                    // the following two bytes must be hex digits

                    byte digit1;
                    byte digit2;
                    if (!Provider.TryPeekHexadecimalDigit(1, out digit1))
                    {
                        throw new Exception("Unexpected byte");
                    }
                    if (!Provider.TryPeekHexadecimalDigit(2, out digit2))
                    {
                        throw new Exception("Unexpected byte");
                    }
                    Provider.Consume(3);
                    byte value = (byte)(digit1 * 16 + digit2);
                    Append(value);
                }
                else
                {
                    byte nextByte = Provider.GetNextByte();
                    Append(nextByte);
                }
            }
        }
    }

    internal class CommentTokeniseContext : TokeniseContext
    {
        public CommentTokeniseContext(ByteProvider provider)
            : base("comment", provider)
        {

        }

        public override IEnumerable<Token> Consume()
        {
            while (true)
            {
                if (!Provider.HasNextByte() || Provider.PeekByte(0) == (byte)'\r' || Provider.PeekByte(0) == (byte)'\n')
                {
                    Provider.Consume(1);
                    yield return new CommentToken(GetToken()); // does not include the leading percent sign (%)
                    yield break; // done with this comment
                }
                else
                {
                    byte nextByte = Provider.GetNextByte();
                    Append(nextByte);
                }
            }
        }
    }

    internal class TokenProvider
    {
        private readonly IEnumerator<Token> _tokens;

        private readonly List<Token> _buffer;

        private bool _endReached;

        public TokenProvider(IEnumerable<Token> tokens)
        {
            _tokens = tokens.GetEnumerator();

            _buffer = new List<Token>(5); // we don't usually need to peek ahead that much

            _endReached = false;
        }

        public bool HasNextToken()
        {
            return PeekNextToken(0).Type != "dummy";
        }

        public Token GetNextToken()
        {
            if (_buffer.Count == 0)
            {
                BufferNextToken();
            }
            var result = _buffer.First();
            _buffer.RemoveAt(0);
            return result;
        }

        public Token PeekNextToken(int delta)
        {
            if (delta < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "Delta value cannot be negative");
            }
            while (delta + 1 > _buffer.Count)
            {
                BufferNextToken();
            }
            return _buffer[delta];
        }

        private void BufferNextToken()
        {
            bool hasNext = _tokens.MoveNext();
            if (hasNext)
            {
                _buffer.Add(_tokens.Current);
            }
            else
            {
                _buffer.Add(new DummyToken());
            }
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

        public static byte[] WhitespaceChars = new byte[] { 0x00, 0x09, 0x0A, 0x0C, 0x0D, 0x20 };

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

        private static object? ExtractIndirectObject(IndexedReader reader, XrefEntry[] xrefTable, uint objectNumber, ushort generation)
        {
            var reference = xrefTable[objectNumber];

            if (reference is null || reference.Generation != generation)
            {
                return null;
            }

            // extract the object found at index, using at least 4 tokens: objectNumber generation "obj" ... "endobj"

            int nextIndex = (int)reference.Offset;

            var tokens = new List<string>();

            uint cmpObjectNumber = reader.GetUInt32Token(ref nextIndex);

            if (cmpObjectNumber != objectNumber)
            {
                return null; // unexpected object number
            }

            ushort cmpGeneration = reader.GetUInt16Token(ref nextIndex);

            if (cmpGeneration != generation)
            {
                return null; // unexpected object generation
            }

            string marker = reader.GetToken(ref nextIndex);

            if (marker != "obj")
            {
                return null; // object marker not found
            }

            string token;

            while ((token = reader.GetToken(ref nextIndex)) != "endobj")
            {
                if (token == "obj")
                {
                    throw new Exception("Objects cannot be nested");
                }

                tokens.Add(token);
            }

            return ParseObject(reader, xrefTable, tokens.ToArray());
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

        private static string GetTokenAtIndex(string[] tokens, int index)
        {
            if (index >= tokens.Length) return string.Empty;
            else return tokens[index];
        }

        private static object? ParseObject(IndexedReader reader, XrefEntry[] xrefTable, IEnumerable<Token> tokens)
        {
            //if (depth > 30)
            //{
            //    return "TOO-DEEP";
            //}

            var parseContext = new ParseContext();

            TokenProvider tokenProvider = new TokenProvider(tokens);

            while (tokenProvider.HasNextToken())
            {
                var nextToken = tokenProvider.GetNextToken();

                //if (nextToken == "stream")
                //{
                //    // TODO implement stream objects

                //    break;
                //}

                // first check if we have either an indirect object (objectNumber generation "obj" ... "endobj)
                // or an indirect reference to an object (objectNumber generation "R")

                if (nextToken is NumericIntegerToken && tokenProvider.PeekNextToken(1) is NumericIntegerToken && tokenProvider.PeekNextToken(2) is IndirectReferenceMarkerToken)
                {
                    var objectNumber = (nextToken as NumericIntegerToken)!.IntegerValue;
                    var generation = (tokenProvider.GetNextToken() as NumericIntegerToken)!.IntegerValue;
                    parseContext.Add(new IndirectReferenceToken(objectNumber, generation));
                }
                else if (nextToken is ArrayBeginToken)
                {
                    parseContext.StartContext("array");
                }
                else if (nextToken is ArrayEndToken)
                {
                    parseContext.EndContext("array");
                }
                else if (nextToken is DictionaryBeginToken)
                {
                    parseContext.StartContext("dictionary");
                }
                else if (nextToken is DictionaryEndToken)
                {
                    parseContext.EndContext("dictionary");
                }
                else
                {
                    parseContext.Add(nextToken);
                }
            }

            return parseContext.GetValue();
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

        public static IEnumerable<Token> Tokenise(ByteProvider provider)
        {
            Stack<TokeniseContext> context = new Stack<TokeniseContext>();

            context.Push(new RootTokeniseContext(provider)); // make sure stack is never empty

            int counter = 0;

            while (provider.HasNextByte())
            {
                if (counter++ > 1000) yield break;

                char test = (char)provider.PeekByte(0);

                if (provider.MatchDelimiter("("))
                {
                    provider.Consume(1);
                    var literalStringContext = new LiteralStringTokeniseContext(provider);
                    foreach (string token in literalStringContext.Consume())
                    {
                        yield return token;
                    }
                }


            }
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
