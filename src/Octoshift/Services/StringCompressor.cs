using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OctoshiftCLI.Services;

public static class StringCompressor
{
    // Zip Solution taken from
    // https://stackoverflow.com/questions/7343465/compression-decompression-string-with-c-sharp
    public static string GZipAndBase64String(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);

        using var msi = new MemoryStream(bytes);
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(mso, CompressionMode.Compress))
        {
            msi.CopyTo(gs);
        }

        return Convert.ToBase64String(mso.ToArray());
    }
}
