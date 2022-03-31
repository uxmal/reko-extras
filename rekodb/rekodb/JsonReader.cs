using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class JsonReader
    {
        private readonly Stream stream;
        private readonly Stack<Scope> scopes;
        private readonly List<byte> buffer;
        private JsonToken token;
        private int cPrev;

        public JsonReader(Stream stream)
        {
            this.stream = stream;
            this.scopes = new Stack<Scope>();
            this.buffer = new List<byte>();
            this.cPrev = -1;
        }

        public JsonToken Read()
        {
            if (this.token == JsonToken.Eof)
            {
                return ReadToken();
            }
            var t = this.token;
            this.token = JsonToken.Eof;
            return t;
        }

        public JsonToken Peek()
        {
            if (this.token == JsonToken.Eof)
            {
                this.token = ReadToken();
            }
            return this.token;
        }

        private JsonToken ReadToken()
        {
            Scope scope;
            var st = State.Default;
            int cHex=0;
            uint hex = 0;
            for (; ;)
            {
                int c;
                if (cPrev != -1)
                {
                    c = cPrev;
                    cPrev = -1;
                }
                else
                {
                    c = stream.ReadByte();
                }
                switch (st)
                {
                case State.Default:
                    switch (c)
                    {
                    case -1: return JsonToken.Eof;
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        break;
                    case '[': 
                        scopes.Push(Scope.BeginList);
                        return JsonToken.BeginList;
                    case ']':
                        if (!scopes.TryPop(out scope) || scope != Scope.BeginList)
                            throw new BadImageFormatException();
                        return Value(JsonToken.EndList);
                    case '{':
                        scopes.Push(Scope.InsideObject);
                        return JsonToken.BeginObject;
                    case '}':
                        if (scopes.TryPop(out scope) &&
                            (scope == Scope.InsideObject ||
                             scope == Scope.PropertyValue))
                        {
                            return Value(JsonToken.EndObject);
                        }
                        throw new BadImageFormatException();
                    case ',':
                        if (!scopes.TryPop(out scope))
                            throw new BadImageFormatException();
                        if (scope == Scope.BeginList)
                        {
                            scopes.Push(Scope.ListComma);
                        }
                        else if (scope == Scope.PropertyValue)
                        {
                            scopes.Push(Scope.ObjectComma);
                        }
                        else
                            throw new BadImageFormatException();
                        break;
                    case ':':
                        if (!scopes.TryPop(out scope) || scope != Scope.PropertyName)
                            throw new BadImageFormatException();
                        scopes.Push(Scope.Colon);
                        break;
                    case '-':
                        buffer.Clear();
                        buffer.Add((byte)c);
                        st = State.NumberStart;
                        break;
                    case '0':
                        buffer.Clear();
                        buffer.Add((byte)c);
                        st = State.NumberZero;
                        break;
                    case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        buffer.Clear();
                        buffer.Add((byte)c);
                        st = State.NumberInt;
                        break;
                    case '"':
                        buffer.Clear();
                        st = State.String;
                        break;
                    default:
                        return Unexpected(c);
                    }
                    break;
                case State.NumberStart:
                    switch (c)
                    {
                    case -1:
                        throw new BadImageFormatException();
                    case '0':
                        buffer.Add((byte)c);
                        st = State.NumberZero;
                        break;
                    default:
                        if (char.IsDigit((char)c))
                        {
                            buffer.Add((byte)c);
                            st = State.NumberInt;
                        }
                        else
                        {
                            return Unexpected(c);
                        }
                        break;
                    }
                    break;
                case State.NumberInt:
                    switch (c)
                    {
                    case -1:
                        cPrev = c;
                        return Value(JsonToken.Number);
                    case '.':
                        buffer.Add((byte)c);
                        st = State.NumberFraction;
                        break;
                    case 'e':
                    case 'E':
                        buffer.Add((byte)c);
                        st = State.NumberExponent;
                        break;
                    default:
                        if (char.IsDigit((char)c))
                        {
                            buffer.Add((byte)c);
                        }
                        else
                        {
                            cPrev = c;
                            return Value(JsonToken.Number);
                        }
                        break;
                    }
                    break;
                case State.NumberZero:
                    switch (c)
                    {
                    case -1:
                        return JsonToken.Number;
                    case '.':
                        buffer.Append((byte)c);
                        st = State.NumberFraction;
                        break;
                    case 'e':
                    case 'E':
                        buffer.Append((byte)c);
                        st = State.NumberExponent;
                        break;
                    default:
                        if (char.IsDigit((char)c))
                        {
                            buffer.Add((byte)c);
                        }
                        else
                        {
                            cPrev = c;
                            return Value(JsonToken.Number);
                        }
                        break;
                    }
                    break;
                case State.NumberFraction:
                    switch (c)
                    {
                    case -1:
                        throw new BadImageFormatException();
                    case 'e':
                    case 'E':
                        buffer.Add((byte)c);
                        st = State.NumberExponent;
                        break;
                    default:
                        if (char.IsNumber((char)c))
                        {
                            buffer.Add((byte)c);
                            st = State.NumberFractionTail;
                            break;
                        }
                        throw new BadImageFormatException();
                    }
                    break;
                case State.NumberFractionTail:
                    switch (c)
                    {
                    case -1:
                        return JsonToken.Number;
                    case 'e':
                    case 'E':
                        buffer.Add((byte)c);
                        st = State.NumberExponent;
                        break;
                    default:
                        if (char.IsNumber((char)c))
                        {
                            buffer.Add((byte)c);
                        }
                        else
                        {
                            cPrev = c;
                            return Value(JsonToken.Number);
                        }
                        break;
                    }
                    break;
                case State.NumberExponent:
                    switch (c)
                    {
                    case -1:
                        throw new BadImageFormatException();
                    case '+':
                    case '-':
                        buffer.Add((byte)c);
                        st = State.NumberExponentTail;
                        break;
                    default:
                        if (char.IsDigit((char)c))
                        {
                            buffer.Add((byte)c);
                            st = State.NumberExponentTail;
                        }
                        else
                        {
                            throw new BadImageFormatException();
                        }
                        break;
                    }
                    break;
                case State.NumberExponentTail:
                    switch (c)
                    {
                    case -1:
                        return JsonToken.Number;
                    default:
                        if (char.IsNumber((char)c))
                        {
                            buffer.Add((byte)c);
                        }
                        else
                        {
                            cPrev = c;
                            return Value(JsonToken.Number);
                        }
                        break;
                    }
                    break;
                case State.String:
                    switch (c)
                    {
                    case -1:
                        throw new BadImageFormatException();
                    case '"':
                        return Value(JsonToken.String);
                    case '\\':
                        st = State.StringEscape;
                        break;
                    default:
                        if (!char.IsControl((char)c))
                        {
                            buffer.Add((byte)c);
                        }
                        else
                            throw new BadImageFormatException();
                        break;
                    }
                    break;
                case State.StringEscape:
                    switch (c)
                    {
                    case -1:
                    default:
                        throw new BadImageFormatException();
                    case '"': buffer.Append((byte)'"'); break;
                    case '\\': buffer.Append((byte)'\\'); break;
                    case 'b': buffer.Append((byte)'\b'); break;
                    case 'f': buffer.Append((byte)'\f'); break;
                    case 'n': buffer.Append((byte)'\n'); break;
                    case 'r': buffer.Append((byte)'\r'); break;
                    case 't': buffer.Append((byte)'\t'); break;
                    case 'u':
                        cHex = 0;
                        hex = 0;
                        st = State.StringEscapeHex;
                        break;
                    }
                    break;
                case State.StringEscapeHex:
                    switch (c)
                    {
                    default:
                        throw new BadImageFormatException();
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        hex = hex * 16 + (uint)(c - '0');
                        if (++cHex == 4)
                        {
                            AddUtf8(hex);
                            st = State.String;
                        }
                        break;
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f':
                        hex = hex * 16 + (uint)(c - 'a' + 10u);
                        if (++cHex == 4)
                        {
                            AddUtf8(hex);
                            st = State.String;
                        }
                        break;
                    case 'A': case 'B': case 'C': case 'D': case 'E': case 'F':
                        hex = hex * 16 + (uint)(c - 'A' + 10u);
                        if (++cHex == 4)
                        {
                            AddUtf8(hex);
                            st = State.String;
                        }
                        break;
                    }
                    break;
                }
            }
        }

        public bool TryGetInt32(out int value)
        {
            return Utf8Parser.TryParse(CollectionsMarshal.AsSpan(buffer), out value, out _);
        }

        public bool TryGetDouble(out double value)
        {
            return Utf8Parser.TryParse(CollectionsMarshal.AsSpan(buffer), out value, out _);
        }

        private JsonToken Value(JsonToken token)
        {
            if (scopes.TryPeek(out var scope))
            {
                switch (scope)
                {
                case Scope.BeginList:
                    break;
                case Scope.InsideObject:
                case Scope.ObjectComma:
                    if (token != JsonToken.String)
                        throw new BadImageFormatException();
                    scopes.Pop();
                    scopes.Push(Scope.PropertyName);
                    return JsonToken.PropertyName;
                case Scope.ListComma:
                    scopes.Pop();
                    scopes.Push(Scope.BeginList);
                    break;
                case Scope.PropertyName:
                    throw new BadImageFormatException();
                case Scope.Colon:
                    scopes.Pop();
                    scopes.Push(Scope.PropertyValue);
                    break;
                default:
                    throw new NotImplementedException($"Scope: {scope}");
                }
            }
            return token;
        }

        private void AddUtf8(uint hex)
        {
            if (hex < 0x80)
            {
                buffer.Add((byte)hex);
            }
            else if (hex < 0x800)
            {
                buffer.Add((byte)(0xC0 | (hex >> 6) & 0x1Fu));
                buffer.Add((byte)(0x80 | hex & 0x3Fu));
            }
            else if (hex < 0x1_0000)
            {
                buffer.Add((byte)(0xE0 | (hex >> 12) & 0x0Fu));
                buffer.Add((byte)(0x80 | (hex >> 6) & 0x3Fu));
                buffer.Add((byte)(0x80 | hex & 0x3Fu));
            }
            else if (hex < 0x11_0000)
            {
                buffer.Add((byte)(0xF0 | (hex >> 18) & 0x07u));
                buffer.Add((byte)(0x80 | (hex >> 12) & 0x3Fu));
                buffer.Add((byte)(0x80 | (hex >> 6) & 0x3Fu));
                buffer.Add((byte)(0x80 | hex & 0x3Fu));
            }
            else
                throw new BadImageFormatException();
        }

        private static JsonToken Unexpected(int c)
        {
            throw new NotImplementedException($"'{(char)c}' (U+{c:X4}) not implemented.");
        }



        public string GetString()
        {
            return Encoding.UTF8.GetString(CollectionsMarshal.AsSpan(buffer));
        }

        private enum State
        {
            Default,
            NumberZero,
            NumberStart,
            NumberInt,
            NumberFraction,
            NumberFractionTail,
            NumberExponent,
            NumberExponentTail,
            String,
            StringEscape,
            StringEscapeHex,
        }

        private enum Scope
        {
            BeginList,          // '['
            ListComma,          // '[' value ','
            InsideObject,       // '{'
            PropertyName,       // '{' str
            Colon,              // '{' str ':'
            PropertyValue,      // '{' str ':' value
            ObjectComma,        // '{' str ':' value ','
        }
    }

    public enum JsonToken
    {
        Eof,
        BeginList,
        EndList,
        BeginObject,
        EndObject,
        PropertyName,
        Number,
        String,
    }
}
