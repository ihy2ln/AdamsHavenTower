using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// A small JSON reader for imported files (glTF headers). JsonUtility needs fixed classes and cannot tell a missing
// index from index 0, so glTF is read into plain objects: Dictionary<string, object>, List<object>, double, string,
// bool and null.
public static class MediaJson
{
    public static object Parse(string text)
    {
        int i = 0;
        object value = Value(text, ref i);
        return value;
    }

    public static Dictionary<string, object> Obj(object o, string key)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
    }

    public static List<object> Arr(object o, string key)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) ? v as List<object> : null;
    }

    public static int Int(object o, string key, int fallback = -1)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) && v is double ? (int)(double)v : fallback;
    }

    public static float Num(object o, string key, float fallback = 0f)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) && v is double ? (float)(double)v : fallback;
    }

    public static string Str(object o, string key)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) ? v as string : null;
    }

    public static float[] Floats(object o, string key)
    {
        var list = Arr(o, key);
        if (list == null) return null;
        var result = new float[list.Count];
        for (int i = 0; i < list.Count; i++) result[i] = list[i] is double ? (float)(double)list[i] : 0f;
        return result;
    }

    private static void Skip(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static object Value(string s, ref int i)
    {
        Skip(s, ref i);
        if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = String(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new FormatException("Expected ':' at " + i);
                i++;
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("Expected ',' or '}' at " + i);
            }
        }
        if (c == '[')
        {
            var list = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("Expected ',' or ']' at " + i);
            }
        }
        if (c == '"') return String(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        if (i == start) throw new FormatException("Unexpected '" + c + "' at " + i);
        return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static string String(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("Expected '\"' at " + i);
        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        throw new FormatException("Unterminated string.");
    }
}
