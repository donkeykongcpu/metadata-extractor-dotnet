// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public abstract class Token : IEquatable<Token>
    {
        public abstract string Type { get; }

        public byte[] Value { get; }

        public int StartIndex { get; }

        protected Token(byte[] value, int startIndex)
        {
            Value = value;

            StartIndex = startIndex;
        }

        protected Token(string value, int startIndex)
        {
            // value should only contain 1-byte characters

            Value = value.ToCharArray().Select(x => (byte)x).ToArray();

            StartIndex = startIndex;
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
        public DummyToken() : base("dummy", -1) { }
    }

    internal class IndirectReferenceMarkerToken : Token
    {
        public override string Type => "R";
        public IndirectReferenceMarkerToken(int startIndex) : base("R", startIndex) { }
    }

    internal class IndirectObjectBeginToken : Token
    {
        public override string Type => "obj";
        public IndirectObjectBeginToken(int startIndex) : base("obj", startIndex) { }
    }

    internal class IndirectObjectEndToken : Token
    {
        public override string Type => "endobj";
        public IndirectObjectEndToken(int startIndex) : base("endobj", startIndex) { }
    }

    internal class StreamBeginToken : Token
    {
        public override string Type => "stream";
        public StreamBeginToken(int startIndex) : base("stream", startIndex) { }
    }

    internal class NullToken : Token
    {
        public override string Type => "null";
        public NullToken(int startIndex) : base("null", startIndex) { }
        public override string ToString()
        {
            return "null";
        }
    }

    internal class BooleanToken : Token
    {
        public bool BooleanValue { get; }

        public override string Type => "boolean";

        public BooleanToken(bool value, int startIndex) : base(value ? "true" : "false", startIndex)
        {
            BooleanValue = value;
        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(Value);
        }
    }

    public class NumericIntegerToken : Token
    {
        public int IntegerValue { get; }

        public override string Type => "numeric-integer";

        public NumericIntegerToken(int value, byte[] rawValue, int startIndex)
            : base(rawValue, startIndex)
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

        public NumericRealToken(decimal value, byte[] rawValue, int startIndex)
            : base(rawValue, startIndex)
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
        public ArrayBeginToken(int startIndex) : base("[", startIndex) { }
    }

    internal class ArrayEndToken : Token
    {
        public override string Type => "array-end";
        public ArrayEndToken(int startIndex) : base("]", startIndex) { }
    }

    internal class DictionaryBeginToken : Token
    {
        public override string Type => "dictionary-begin";
        public DictionaryBeginToken(int startIndex) : base("<<", startIndex) { }
    }

    internal class DictionaryEndToken : Token
    {
        public override string Type => "dictionary-end";
        public DictionaryEndToken(int startIndex) : base(">>", startIndex) { }
    }

    public class StringToken : Token
    {
        public override string Type => "string";

        public StringValue StringValue => new StringValue(Value); // TODO encoding is context-specific

        public StringToken(byte[] value, int startIndex)
            : base(value, startIndex)
        {

        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(Value); // TODO encoding is context-specific
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

        public StringValue StringValue => new StringValue(Value, Encoding.UTF8);

        public NameToken(byte[] value, int startIndex)
            : base(value, startIndex)
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

        public CommentToken(byte[] value, int startIndex)
            : base(value, startIndex)
        {
            // the value does not include the leading slash (/)
        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(Value); // only used for debugging
        }
    }
}
