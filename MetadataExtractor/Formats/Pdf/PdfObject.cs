// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public abstract class PdfObject
    {
        public abstract string Type { get; }

        public abstract object? GetValue();

        public abstract void Add(PdfObject pdfObject);
    }

    public class PdfRoot : PdfObject
    {
        private PdfObject? _rootValue;

        public override string Type => "root";

        public PdfRoot()
        {
            _rootValue = null;
        }

        public override object? GetValue()
        {
            if (_rootValue is null)
            {
                throw new Exception("Value was not set");
            }
            return _rootValue;
        }

        public PdfObject GetRootValue()
        {
            if (_rootValue is null)
            {
                throw new Exception("Value was not set");
            }
            return _rootValue;
        }

        public override void Add(PdfObject pdfObject)
        {
            if (_rootValue is not null)
            {
                throw new Exception("Value already set");
            }
            _rootValue = pdfObject;
        }
    }

    public class PdfScalarValue : PdfObject
    {
        private readonly string _type;

        private readonly object? _value;

        public override string Type => _type;

        public static PdfScalarValue FromIndirectReference(uint objectNumber, ushort generation)
        {
            return new PdfScalarValue("indirect-reference", new IndirectReference(objectNumber, generation));
        }

        public static PdfScalarValue FromIndirectReference(int objectNumber, int generation)
        {
            return new PdfScalarValue("indirect-reference", new IndirectReference(objectNumber, generation));
        }

        public static PdfScalarValue FromToken(Token token)
        {
            if (token is NullToken)
            {
                return new PdfScalarValue("null", null);
            }
            else if (token is BooleanToken booleanToken)
            {
                return new PdfScalarValue("boolean", booleanToken.BooleanValue);
            }
            else if (token is NumericIntegerToken numericIntegerToken)
            {
                return new PdfScalarValue("numeric-integer", numericIntegerToken.IntegerValue);
            }
            else if (token is NumericRealToken numericRealToken)
            {
                return new PdfScalarValue("numeric-real", numericRealToken.RealValue);
            }
            else if (token is StringToken stringToken)
            {
                return new PdfScalarValue("string", stringToken.StringValue); // TODO encoding is context-specific
            }
            else if (token is NameToken nameToken)
            {
                return new PdfScalarValue("name", nameToken.StringValue);
            }
            else
            {
                throw new Exception($"Unexpected token type {token.Type}");
            }
        }

        private PdfScalarValue(string type, object? value)
        {
            _type = type;

            _value = value;
        }

        public override object? GetValue() => _value;

        public override string ToString()
        {
            if (Type == "name")
            {
                // names are not usually intended to be printed,
                // but when they are, their encoding is supposed to be UTF8
                if (_value is null)
                {
                    throw new Exception("Value cannot be null");
                }
                return ((StringValue)_value).ToString(Encoding.UTF8);
            }
            return base.ToString();
        }

        public override void Add(PdfObject pdfObject)
        {
            throw new Exception("Cannot nest scalar values");
        }
    }

    public class PdfIndirectObject : PdfObject
    {
        private object? _value;

        private bool _valueWasSet;

        public override string Type => "indirect-object";

        public PdfIndirectObject()
        {
            _valueWasSet = false;
        }

        public override object? GetValue()
        {
            if (!_valueWasSet)
            {
                throw new Exception("Value was not set");
            }
            return _value;
        }

        public override void Add(PdfObject pdfObject)
        {
            if (_valueWasSet)
            {
                throw new Exception("Value already set");
            }
            _value = pdfObject;
            _valueWasSet = true;
        }
    }

    public class PdfArray : PdfObject
    {
        private readonly List<PdfObject> _array;

        public override string Type => "array";

        public PdfArray()
        {
            _array = new List<PdfObject>();
        }

        public PdfArray(IEnumerable<PdfObject> values)
        {
            _array = new List<PdfObject>(values);
        }

        public override object? GetValue()
        {
            return _array;
        }

        public override void Add(PdfObject pdfObject)
        {
            _array.Add(pdfObject);
        }
    }

    public class PdfDictionary : PdfObject
    {
        private readonly Dictionary<string, PdfObject> _dictionary;

        private string? _currentKey = null;

        public override string Type => "dictionary";

        public PdfDictionary()
        {
            _dictionary = new Dictionary<string, PdfObject>();
        }

        public PdfDictionary(IDictionary<string, PdfObject> values)
        {
            _dictionary = new Dictionary<string, PdfObject>(values);
        }

        public override object? GetValue()
        {
            return _dictionary;
        }

        public override void Add(PdfObject pdfObject)
        {
            if (pdfObject.Type == "name")
            {
                if (_currentKey is not null)
                {
                    _dictionary.Add(_currentKey, pdfObject); // names can be values, too, in which case their encoding is probably UTF8
                    _currentKey = null;
                }
                else
                {
                    _currentKey = pdfObject.ToString(); // documented dictionary keys are probably ASCII
                }
            }
            else
            {
                if (_currentKey is null)
                {
                    return;
                }
                if (pdfObject.Type != "null")
                {
                    _dictionary.Add(_currentKey, pdfObject);
                }
                _currentKey = null;
            }
        }
    }
}
