/* Util.cs
Various helper functions for byte and string processing.
*/

using System;
using System.IO;
using System.Collections.Generic;

//to be thrown when error with quote string processing
[Serializable]
class QuoteException : Exception {
    public QuoteException() : base() {}
    public QuoteException(string message) : base(message) {}
    public QuoteException(string message, Exception inner) : base(message, inner) {}

    protected QuoteException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) {}
}

//to be thrown when error with index sizing for Entry() functions
[Serializable]
class IndexException : Exception {
    public IndexException() : base() {}
    public IndexException(string message) : base(message) {}
    public IndexException(string message, Exception inner) : base(message, inner) {}

    protected IndexException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) {}
}

class Util {
    //appends bytes b to a file specified by path
    public static void AppendAllBytes(string path, byte[] b) {
        var stream = new FileStream(path, FileMode.Append);
        stream.Write(b, 0, b.Length);
    }

    //retrieves n bytes of index'th (n+1)-byte segment
    public static byte[] ReadNByteEntry(string path, uint index, uint n) {
        byte[] entry = new byte[n];
        byte[] data = File.ReadAllBytes(path);

        uint real_n = n + 1;

        if (data.Length % real_n != 0)
            throw new Exception("File data size is not a multiple of n");

        if (data.Length < real_n * (index + 1))
            throw new IndexException("File data size is too small");

        Array.Copy(data, index * real_n, entry, 0, n);
        return entry;
    }

    //writes n bytes to the first available n-byte segment ((n+1)'th byte
    //is 1 for segment used, 0 for segment not used); assumes byte array
    //has length of n, and file is segmented by n bytes if it already exists
    public static void AddNByteEntry(string path, byte[] b, uint n) {
        if (b.Length != n)
            throw new Exception("Byte array b is not of length n");
        
        try {
            uint real_n = n + 1;

            byte[] data = File.ReadAllBytes(path);
            if (data.Length % real_n != 0)
                throw new Exception("File data size is not a multiple of n");
            
            for (int i = 0; i < data.Length / real_n; i += 1) {
                if (data[(i * real_n) + (real_n - 1)] == 0) {
                    //write n bytes into data starting at i * n,
                    //including (n+1)'th 0x01 byte for segment used
                    Array.Copy(b, 0, data, i * real_n, n);
                    data[(i * real_n) + (real_n - 1)] = 1;
                    return;
                }
            }

            //no empty entry found; append bytes to end
            byte[] new_entry = new byte[real_n];
            Array.Copy(b, 0, new_entry, 0, n);
            new_entry[real_n - 1] = 1;
            Util.AppendAllBytes(path, b);

        } catch (FileNotFoundException) {
            File.WriteAllBytes(path, b);
        }
    }

    //sets (n+1)'th byte of index'th (n+1)-byte segment to 0x00
    public static void RemoveNByteEntry(string path, uint index, uint n) {
        byte[] data = File.ReadAllBytes(path);

        uint real_n = n + 1;

        if (data.Length % real_n != 0)
            throw new Exception("File data size is not a multiple of n");

        if (data.Length < real_n * (index + 1))
            throw new IndexException("File data size is too small");
        
        data[(index * real_n) + (real_n - 1)] = 0;
        File.WriteAllBytes(path, data);
    }

    //little endian
    public static ulong DecodeUInt64(byte[] bytes) {
        if (bytes.Length < 8)
            return 0;
        return
            (ulong)(bytes[0]) |
            (ulong)(bytes[1]) << 8 |
            (ulong)(bytes[2]) << 16 |
            (ulong)(bytes[3]) << 24 |
            (ulong)(bytes[4]) << 32 |
            (ulong)(bytes[5]) << 40 |
            (ulong)(bytes[6]) << 48 |
            (ulong)(bytes[7]) << 56;
    }

    //little endian
    public static byte[] EncodeUInt64(ulong x) {
        byte[] b = new byte[8];
        for (int i = 0; i < 8; i += 1) {
            b[i] = (byte)(x >> (8 * i));
        }
        return b;
    }

    //returns the Unicode character "regional_indicator_a" shifted by i
    public static string GetUnicodeLetterI(int i) {
        if (i >= 26)
            return "";
        String unicode_a_str = "1F1E6";
        int unicode_a_str_int = int.Parse(unicode_a_str, System.Globalization.NumberStyles.HexNumber);
        unicode_a_str_int += (int)i;
        return Char.ConvertFromUtf32(unicode_a_str_int);
    }

    public static ulong GetSubstringUInt64(string str) {
        String str_object = new String(str);
        if (str_object.StartsWith("<@!") && ulong.TryParse(str_object.Substring(3, str_object.Length - 4), out ulong id))
            return id;
        else
            return 0;
    }

    public static string[] GetQuoteSubstrings(string str) {
        String str_object = new String(str);
        List<string> quotes = new List<string>();
        bool srch = false;
        int i_start = 0;
        int i = 0;

        foreach (char c in str) {
            if (c == '\"') {
                if (!srch) {
                    //start searching for quote
                    srch = true;
                    i_start = i;
                } else {
                    //end found; push to list if length is > 0 (without quotes)
                    srch = false;
                    if (i - i_start > 1)
                        quotes.Add(str_object.Substring(i_start + 1, (i - 1) - i_start).ToString());
                }
            }

            i++;
        }

        if (srch) {
            throw new QuoteException("Open quote");
        }

        return quotes.ToArray();
    }
}