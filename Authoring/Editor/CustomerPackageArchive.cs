namespace Threadlight.Authoring.Editor
{
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Streams Unity's freshly generated tar/gzip package without extracting paths.
/// Only prevalidated asset entries change; all other entry bytes are preserved.
/// </summary>
internal static class CustomerPackageArchive
{
    public static void Rewrite(string source, string destination, Dictionary<string, byte[]> replacements)
    {
        var remaining = new HashSet<string>(replacements.Keys, StringComparer.Ordinal);
        using (var inputFile = File.OpenRead(source))
        using (var input = new GZipStream(inputFile, CompressionMode.Decompress))
        using (var outputFile = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
        using (var output = new GZipStream(outputFile, CompressionLevel.Optimal))
        {
            byte[] header = new byte[512];
            byte[] buffer = new byte[81920];
            while (true)
            {
                ReadExactly(input, header, 512);
                bool end = true;
                for (int i = 0; i < header.Length; i++) if (header[i] != 0) { end = false; break; }
                if (end)
                {
                    output.Write(header, 0, 512);
                    input.CopyTo(output);
                    break;
                }
                long expectedChecksum = Octal(header, 148, 8);
                long actualChecksum = 0;
                for (int i = 0; i < 512; i++) actualChecksum += i >= 148 && i < 156 ? 32 : header[i];
                if (actualChecksum != expectedChecksum) throw new InvalidDataException("Unity package header checksum mismatch.");
                string name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                long size = Octal(header, 124, 12);
                if (replacements.TryGetValue(name, out byte[] replacement))
                {
                    if (!remaining.Remove(name) || (header[156] != 0 && header[156] != (byte)'0'))
                        throw new InvalidDataException("Ambiguous Unity package asset entry: " + name);
                    Copy(input, null, size + Padding(size), buffer);
                    byte[] length = Encoding.ASCII.GetBytes(Convert.ToString(replacement.LongLength, 8).PadLeft(11, '0') + "\0");
                    Array.Copy(length, 0, header, 124, 12);
                    for (int i = 148; i < 156; i++) header[i] = 32;
                    long checksum = 0;
                    foreach (byte value in header) checksum += value;
                    byte[] encoded = Encoding.ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ");
                    Array.Copy(encoded, 0, header, 148, 8);
                    output.Write(header, 0, 512);
                    output.Write(replacement, 0, replacement.Length);
                    output.Write(new byte[512], 0, (int)Padding(replacement.LongLength));
                }
                else
                {
                    output.Write(header, 0, 512);
                    Copy(input, output, size + Padding(size), buffer);
                }
            }
        }
        if (remaining.Count != 0) throw new InvalidDataException("Unity did not export every converted prefab entry.");
    }

    private static long Padding(long length) => (512 - length % 512) % 512;
    private static long Octal(byte[] header, int offset, int count)
    {
        string text = Encoding.ASCII.GetString(header, offset, count).Trim('\0', ' ');
        return string.IsNullOrEmpty(text) ? 0 : Convert.ToInt64(text, 8);
    }
    private static void ReadExactly(Stream input, byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int current = input.Read(buffer, read, count - read);
            if (current == 0) throw new EndOfStreamException("Incomplete Unity package.");
            read += current;
        }
    }
    private static void Copy(Stream input, Stream output, long count, byte[] buffer)
    {
        while (count > 0)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(count, buffer.Length));
            if (read == 0) throw new EndOfStreamException("Incomplete Unity package entry.");
            output?.Write(buffer, 0, read);
            count -= read;
        }
    }
}
}
