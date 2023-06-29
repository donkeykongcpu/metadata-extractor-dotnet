// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public abstract class PdfObject
    {
    }

    public interface IPdfContainer
    {
        public abstract string Type { get; }

        void Nest<T>(PdfContainer<T> pdfContainer);

        void Add(PdfObject pdfObject);
    }

    public abstract class PdfContainer<T> : PdfObject, IPdfContainer
    {
        public abstract string Type { get; }

        public abstract T GetValue();

        public void Nest<U>(PdfContainer<U> pdfContainer)
        {
            Add(pdfContainer);
        }

        public abstract void Add(PdfObject pdfObject);
    }

    public abstract class PdfScalarObject<T> : PdfObject
    {
        public T Value { get; private set; }

        protected PdfScalarObject(T value)
        {
            Value = value;
        }
    }

    #region Scalar objects

    public class PdfNull : PdfObject
    {
    }

    public class PdfBoolean : PdfScalarObject<bool>
    {
        public PdfBoolean(bool value) : base(value)
        {
        }
    }

    public class PdfNumericInteger : PdfScalarObject<int>
    {
        public PdfNumericInteger(int value) : base(value)
        {
        }
    }

    public class PdfNumericReal : PdfScalarObject<decimal>
    {
        public PdfNumericReal(decimal value) : base(value)
        {
        }
    }

    public class PdfString : PdfScalarObject<StringValue>
    {
        public PdfString(StringValue value) : base(value)
        {
        }

        public string ToASCIIString()
        {
            return Value.ToString(Encoding.ASCII);
        }

        public string ToUTF8String()
        {
            return Value.ToString(Encoding.UTF8);
        }
    }

    public class PdfName : PdfString
    {
        public PdfName(StringValue value) : base(value)
        {
        }
    }

    public class PdfIndirectReference : PdfScalarObject<ObjectIdentifier>
    {
        public PdfIndirectReference(ObjectIdentifier value) : base(value)
        {
        }

        public PdfIndirectReference(uint objectNumber, ushort generationNumber)
           : base(new ObjectIdentifier(objectNumber, generationNumber))
        {
        }

        public PdfIndirectReference(int objectNumber, int generationNumber)
            : base(new ObjectIdentifier(objectNumber, generationNumber))
        {
        }
    }

    public class PdfStream : PdfObject
    {
        // from the spec:
        // All streams shall be indirect objects and the stream dictionary shall be a direct object

        public ObjectIdentifier Identifier { get; }

        public PdfDictionary StreamDictionary { get; }

        public int StreamStartIndex { get; }

        public PdfStream(ObjectIdentifier identifier, PdfDictionary streamDictionary, int streamStartIndex)
        {
            Identifier = identifier;

            StreamDictionary = streamDictionary;

            StreamStartIndex = streamStartIndex;
        }

        public PdfStream(uint objectNumber, ushort generationNumber, PdfDictionary streamDictionary, int streamStartIndex)
            : this(new ObjectIdentifier(objectNumber, generationNumber), streamDictionary, streamStartIndex)
        {
        }

        public PdfStream(int objectNumber, int generationNumber, PdfDictionary streamDictionary, int streamStartIndex)
            : this(new ObjectIdentifier(objectNumber, generationNumber), streamDictionary, streamStartIndex)
        {
        }
    }

    #endregion Scalar objects

    #region Containers

    public class PdfRoot : PdfContainer<PdfObject>
    {
        private PdfObject? _value;

        public override PdfObject GetValue()
        {
            if (_value is null)
            {
                throw new Exception("Value was not set");
            }
            return _value;
        }

        public override string Type => "root";

        public ObjectIdentifier Identifier { get; }

        public PdfRoot()
        {
            _value = null;
        }

        public PdfRoot(PdfObject? value)
        {
            _value = value;
        }

        public override void Add(PdfObject pdfObject)
        {
            if (_value is not null)
            {
                throw new Exception("Value already set");
            }
            _value = pdfObject;
        }

        public void ReplaceValue(PdfObject pdfObject)
        {
            _value = pdfObject;
        }
    }

    public class PdfIndirectObject : PdfRoot
    {
        public override string Type => "indirect-object";

        public ObjectIdentifier Identifier { get; }

        public PdfIndirectObject(uint objectNumber, ushort generationNumber)
            : base()
        {
            Identifier = new ObjectIdentifier(objectNumber, generationNumber);
        }

        public PdfIndirectObject(int objectNumber, int generationNumber)
            : base()
        {
            Identifier = new ObjectIdentifier(objectNumber, generationNumber);
        }

        public PdfIndirectObject(int objectNumber, int generationNumber, PdfObject? value)
            : base(value)
        {
            Identifier = new ObjectIdentifier(objectNumber, generationNumber);
        }
    }

    public class PdfArray : PdfContainer<List<PdfObject>>
    {
        private readonly List<PdfObject> _value;

        public override List<PdfObject> GetValue()
        {
            return _value;
        }

        public override string Type => "array";

        public PdfArray()
        {
            _value = new List<PdfObject>();
        }

        public PdfArray(IEnumerable<PdfObject> values)
        {
            _value = new List<PdfObject>(values);
        }

        public override void Add(PdfObject pdfObject)
        {
            _value.Add(pdfObject);
        }
    }

    public class PdfDictionary : PdfContainer<Dictionary<string, PdfObject>>
    {
        private readonly Dictionary<string, PdfObject> _value;

        public override Dictionary<string, PdfObject> GetValue()
        {
            return _value;
        }

        private string? _currentKey = null;

        public override string Type => "dictionary";

        public PdfDictionary()
        {
            _value = new Dictionary<string, PdfObject>();
        }

        public PdfDictionary(IDictionary<string, PdfObject> values)
        {
            _value = new Dictionary<string, PdfObject>(values);
        }

        public override void Add(PdfObject pdfObject)
        {
            if (pdfObject is PdfName name)
            {
                if (_currentKey is not null)
                {
                    _value.Add(_currentKey, pdfObject); // names can be values, too, in which case their encoding is probably UTF8
                    _currentKey = null;
                }
                else
                {
                    _currentKey = name.ToASCIIString(); // documented dictionary keys are probably ASCII
                }
            }
            else
            {
                if (_currentKey is null)
                {
                    return;
                }
                if (pdfObject is not PdfNull)
                {
                    _value.Add(_currentKey, pdfObject);
                }
                _currentKey = null;
            }
        }

        public int GetNumericIntegerForKey(string key)
        {
            if (_value[key] is PdfNumericInteger numericInteger) return numericInteger.Value;
            else throw new Exception($"Value for key {key} is not a numeric integer");
        }
    }

    #endregion Containers
}
