// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    internal class PdfParser
    {
        private class ParseContext
        {
            private readonly Stack<PdfObject> _stack;

            public bool IsRoot => _stack.Peek().Type == "root";

            public bool HasValue => _stack.Peek().HasValue;

            private string ContextType => _stack.Peek().Type;

            public ParseContext()
            {
                _stack = new Stack<PdfObject>();
                _stack.Push(new PdfRoot());
            }

            public void Add(PdfObject value)
            {
                _stack.Peek().Add(value);
            }

            public void Add(Token token)
            {
                _stack.Peek().Add(token);
            }

            public void StartContext(string type)
            {
                PdfObject pdfObject;
                switch (type)
                {
                    case "root": pdfObject = new PdfRoot(); break;
                    case "indirect-object": pdfObject = new PdfIndirectObject(); break;
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

        private readonly ItemProvider<Token> _tokenProvider;

        public PdfParser(ItemProvider<Token> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        public IEnumerable<object?> Parse()
        {
            var parseContext = new ParseContext();

            while (true)
            {
                // first check if we have either an indirect object (objectNumber generation "obj" ... "endobj")
                // or an indirect reference to an object (objectNumber generation "R")

                if (_tokenProvider.PeekNextItem(0) is NumericIntegerToken objectNumberToken
                    && _tokenProvider.PeekNextItem(1) is NumericIntegerToken generationToken
                    )
                {
                    var objectNumber = objectNumberToken.IntegerValue;
                    var generation = generationToken.IntegerValue;

                    if (_tokenProvider.PeekNextItem(2) is IndirectObjectBeginToken)
                    {
                        parseContext.StartContext("indirect-object");
                        _tokenProvider.Consume(3);
                    }
                    else if (_tokenProvider.PeekNextItem(2) is IndirectReferenceMarkerToken)
                    {
                        parseContext.Add(new PdfIndirectReference(objectNumber, generation));
                        _tokenProvider.Consume(3);
                    }
                }

                var nextToken = _tokenProvider.GetNextItem();

                if (nextToken is ArrayBeginToken)
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
                else if (nextToken is IndirectObjectEndToken)
                {
                    parseContext.EndContext("indirect-object");
                }
                else
                {
                    if (parseContext.IsRoot && parseContext.HasValue)
                    {
                        yield return parseContext.GetValue();
                    }
                    parseContext.Add(nextToken);
                    if (nextToken is DummyToken)
                    {
                        yield break;
                    }
                }

              


                //if (nextToken == "stream")
                //{
                //    // TODO implement stream objects

                //    break;
                //}

                //if (nextToken == "xref")
                //{
                //    // TODO implement cross-reference table objects

                //    break;
                //}

                //if (nextToken == "trailer")
                //{
                //    // TODO implement trailer objects

                //    break;
                //}


            }

        }



    }
}
