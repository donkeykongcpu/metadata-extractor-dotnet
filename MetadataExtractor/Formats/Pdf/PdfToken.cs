// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
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

    internal class BooleanToken : Token
    {
        public bool BooleanValue { get; }

        public override string Type => "boolean";

        public BooleanToken(bool value) : base(value ? "true" : "false")
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

        public override string ToString()
        {
            return Encoding.ASCII.GetString(Value); // only used for debugging
        }
    }
}
