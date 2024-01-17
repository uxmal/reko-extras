﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class JsonWriter
    {
        private TextWriter w;
        private Stack<bool> needsSeparator;

        public JsonWriter(TextWriter w)
        {
            this.w = w;
            this.needsSeparator = new Stack<bool>();
        }

        public void BeginList()
        {
            needsSeparator.Push(false);
            w.Write('[');
        }

        public void EndList()
        {
            needsSeparator.Pop();
            w.Write(']');
        }

        public void BeginObject()
        {
            needsSeparator.Push(false);
            w.Write('{');
        }

        public void EndObject()
        {
            needsSeparator.Pop();
            w.Write('}');
        }

        public void WriteList<T>(IEnumerable<T> items, Action<T> itemWriter)
        {
            w.Write('[');
            WriteListContents(items, itemWriter);
            w.Write(']');
        }

        public void WriteList(Action<JsonWriter> writer)
        {
            w.Write('[');
            writer(this);
            w.Write(']');
        }

        public void WriteListContents<T>(IEnumerable<T> items, Action<T> itemWriter)
        {
            bool sep = false;
            foreach (var item in items)
            {
                if (sep)
                    w.Write(',');
                sep = true;
                itemWriter(item);
            }
        }

        private static Dictionary<Type, Action<JsonWriter, object>> atomWriters =
            new Dictionary<Type, Action<JsonWriter, object>>
            {
                { typeof(bool), (js, o) => js.Write((bool)o) },
                { typeof(int), (js, o) => js.Write((int)o) },
                { typeof(uint), (js, o) => js.Write((uint)o) },
                { typeof(long), (js, o) => js.Write((long)o) },
                { typeof(ulong), (js, o) => js.Write((ulong)o) },
                { typeof(string), (js, o) => js.Write((string)o) },
            };

        public void Write(object o)
        {
            if (o == null)
            {
                w.Write("null");
                return;
            }
            Action<JsonWriter, object>? atomWriter;
            if (atomWriters.TryGetValue(o.GetType(), out atomWriter))
            {
                atomWriter(this, o);
                return;
            }
            throw new NotImplementedException(string.Format("Don't know how to write {0}.", o.GetType()));
        }

        public void Write(bool value)
        {
            w.Write(value ? "true" : "false");
        }

        public void Write(ulong u64)
        {
            // Max integer representable with a IEEE 64-bit double
            const ulong MaxExactUInt = (1ul << 53) - 1ul;
            if (u64 > MaxExactUInt)
                Write(u64.ToString());
            else
                w.Write(u64);
        }

        public void Write(long i64)
        {
            // Max integer representable with a IEEE 64-bit double
            const long MaxExactInt = (1L << 53) - 1L;
            if (Math.Abs(i64) > MaxExactInt)
                Write(i64.ToString());
            else
                w.Write(i64);
        }

        public void Write(string s)
        {
            if (s is null)
            {
                w.Write("null");
                return;
            }
            w.Write('"');
            foreach (char c in s)
            {
                switch (c)
                {
                case '\b': w.Write(@"\"); break;
                case '\t': w.Write(@"\"); break;
                case '\n': w.Write(@"\"); break;
                case '\f': w.Write(@"\"); break;
                case '\r': w.Write(@"\"); break;
                case '\"': w.Write(@"\"""); break;
                case '\\': w.Write(@"\\"); break;
                default:
                    if (0 <= c && c < ' ' || 0x7F <= c)
                    {
                        w.Write(@"\u{0:X4}", (int)c);
                    }
                    else
                    {
                        w.Write(c);
                    }
                    break;
                }
            }
            w.Write('"');
        }

        public void WriteListItem(object o)
        {
            WriteSeparator();
            Write(o);
        }

        public void WriteListItem(Action valueWriter)
        {
            WriteSeparator();
            valueWriter();
        }

        public void WriteKeyValue(string key, object value)
        {
            WriteSeparator();
            Write(key);
            w.Write(':');
            Write(value);
        }

        public void WriteKeyValue(string key, Action valueWriter)
        {
            WriteSeparator();
            Write(key);
            w.Write(':');
            valueWriter();
        }

        private void WriteSeparator()
        {
            if (needsSeparator.Peek())
            {
                w.Write(',');
            }
            else
            {
                needsSeparator.Pop();
                needsSeparator.Push(true);
            }
        }
    }
}
