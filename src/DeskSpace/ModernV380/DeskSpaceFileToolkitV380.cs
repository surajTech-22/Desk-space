using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfImage = System.Windows.Controls.Image;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace DeskSpace;

internal static class QrCodeEncoderV380
{
    private sealed record VersionSpec(
        int Version,
        int DataCodewords,
        int EcPerBlock,
        int Group1Blocks,
        int Group1DataLen,
        int Group2Blocks,
        int Group2DataLen,
        int RemainderBits,
        int[] AlignmentCenters);

    // ISO/IEC 18004 Versions 1..10 at Error Correction Level M (15% recovery).
    private static readonly VersionSpec[] SpecsM =
    {
        new(1,  16,  10, 1, 16, 0, 0,  0, Array.Empty<int>()),
        new(2,  28,  16, 1, 28, 0, 0,  7, new[] { 6, 18 }),
        new(3,  44,  26, 1, 44, 0, 0,  7, new[] { 6, 22 }),
        new(4,  64,  18, 2, 32, 0, 0,  7, new[] { 6, 26 }),
        new(5,  86,  24, 2, 43, 0, 0,  7, new[] { 6, 30 }),
        new(6,  108, 16, 4, 27, 0, 0,  7, new[] { 6, 34 }),
        new(7,  124, 18, 4, 31, 0, 0,  0, new[] { 6, 22, 38 }),
        new(8,  154, 22, 2, 38, 2, 39, 0, new[] { 6, 24, 42 }),
        new(9,  182, 22, 3, 36, 2, 37, 0, new[] { 6, 26, 46 }),
        new(10, 216, 26, 4, 43, 1, 44, 0, new[] { 6, 28, 50 })
    };

    private static readonly byte[] GfExp = new byte[512];
    private static readonly byte[] GfLog = new byte[256];

    static QrCodeEncoderV380()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            GfExp[i] = (byte)x;
            GfExp[i + 255] = (byte)x;
            GfLog[x] = (byte)i;
            x <<= 1;
            if ((x & 0x100) != 0)
                x ^= 0x11D;
        }
    }

    private static byte GfMul(byte a, byte b) =>
        (a == 0 || b == 0) ? (byte)0 : GfExp[GfLog[a] + GfLog[b]];

    private static byte[] BuildGeneratorPolynomial(int ecCount)
    {
        byte[] poly = { 1 };
        for (int i = 0; i < ecCount; i++)
        {
            byte root = GfExp[i];
            var next = new byte[poly.Length + 1];
            next[0] = poly[0];
            for (int j = 1; j < poly.Length; j++)
                next[j] = (byte)(poly[j] ^ GfMul(poly[j - 1], root));
            next[poly.Length] = GfMul(poly[^1], root);
            poly = next;
        }
        return poly;
    }

    private static byte[] ComputeReedSolomon(byte[] data, int ecCount)
    {
        byte[] gen = BuildGeneratorPolynomial(ecCount);
        var work = new byte[data.Length + ecCount];
        Buffer.BlockCopy(data, 0, work, 0, data.Length);

        for (int i = 0; i < data.Length; i++)
        {
            byte factor = work[i];
            if (factor == 0)
                continue;
            for (int j = 0; j < gen.Length; j++)
                work[i + j] ^= GfMul(gen[j], factor);
        }

        var ec = new byte[ecCount];
        Buffer.BlockCopy(work, data.Length, ec, 0, ecCount);
        return ec;
    }

    internal static bool[,] EncodeMatrix(string payload)
    {
        if (string.IsNullOrEmpty(payload))
            throw new ArgumentException("QR payload cannot be empty.", nameof(payload));

        byte[] utf8 = Encoding.UTF8.GetBytes(payload);
        const int maxCapacityBytes = 213;
        if (utf8.Length > maxCapacityBytes)
            throw new ArgumentException(
                $"QR payload ({utf8.Length} bytes) exceeds Version 10 ECC-M Byte capacity of {maxCapacityBytes} bytes.",
                nameof(payload));

        VersionSpec? chosen = null;
        foreach (var spec in SpecsM)
        {
            int countBits = spec.Version >= 10 ? 16 : 8;
            int headerBits = 4 + countBits;
            int capBytes = ((spec.DataCodewords * 8) - headerBits) / 8;
            if (utf8.Length <= capBytes)
            {
                chosen = spec;
                break;
            }
        }

        if (chosen == null)
            throw new ArgumentException("QR payload exceeds supported Version 10 capacity.", nameof(payload));

        byte[] dataCodewords = EncodeByteModeData(utf8, chosen);
        byte[] interleaved = BuildInterleavedCodewords(dataCodewords, chosen);

        int size = 17 + 4 * chosen.Version;
        var isFunction = new bool[size, size];
        var baseModules = new bool[size, size];

        DrawFunctionModules(baseModules, isFunction, chosen);
        PlaceDataBits(baseModules, isFunction, interleaved);

        int bestMask = 0;
        int bestPenalty = int.MaxValue;
        bool[,]? bestGrid = null;

        for (int mask = 0; mask < 8; mask++)
        {
            var candidate = (bool[,])baseModules.Clone();
            ApplyMask(candidate, isFunction, mask);
            DrawFormatInfo(candidate, chosen.Version, mask);
            if (chosen.Version >= 7)
                DrawVersionInfo(candidate, chosen.Version);

            int penalty = EvaluatePenalty(candidate);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask = mask;
                bestGrid = candidate;
            }
        }

        _ = bestMask;
        return bestGrid!;
    }

    internal static WriteableBitmap RenderBitmap(
        string payload,
        int pixelsPerModule = 8,
        int quietZoneModules = 4)
    {
        if (pixelsPerModule < 1 || pixelsPerModule > 64)
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule));
        if (quietZoneModules < 0 || quietZoneModules > 32)
            throw new ArgumentOutOfRangeException(nameof(quietZoneModules));

        bool[,] matrix = EncodeMatrix(payload);
        int modules = matrix.GetLength(0);
        int totalModules = modules + quietZoneModules * 2;
        int imgPx = totalModules * pixelsPerModule;
        int stride = imgPx * 4;
        var pixels = new byte[imgPx * stride];

        // Fill white background (BGRA: 255, 255, 255, 255)
        Array.Fill(pixels, (byte)255);

        for (int r = 0; r < modules; r++)
        {
            for (int c = 0; c < modules; c++)
            {
                if (!matrix[r, c])
                    continue;

                int startY = (r + quietZoneModules) * pixelsPerModule;
                int startX = (c + quietZoneModules) * pixelsPerModule;
                for (int dy = 0; dy < pixelsPerModule; dy++)
                {
                    int rowOffset = (startY + dy) * stride;
                    for (int dx = 0; dx < pixelsPerModule; dx++)
                    {
                        int idx = rowOffset + (startX + dx) * 4;
                        pixels[idx] = 0;     // B
                        pixels[idx + 1] = 0; // G
                        pixels[idx + 2] = 0; // R
                        pixels[idx + 3] = 255;
                    }
                }
            }
        }

        var bmp = new WriteableBitmap(imgPx, imgPx, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, imgPx, imgPx), pixels, stride, 0);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] EncodeByteModeData(byte[] payloadBytes, VersionSpec spec)
    {
        int totalBits = spec.DataCodewords * 8;
        var bits = new List<bool>(totalBits);

        static void AppendBits(List<bool> target, int value, int bitCount)
        {
            for (int i = bitCount - 1; i >= 0; i--)
                target.Add(((value >> i) & 1) != 0);
        }

        // Byte mode indicator: 0100
        AppendBits(bits, 0b0100, 4);
        int countBitWidth = spec.Version >= 10 ? 16 : 8;
        AppendBits(bits, payloadBytes.Length, countBitWidth);

        foreach (byte b in payloadBytes)
            AppendBits(bits, b, 8);

        int terminator = Math.Min(4, totalBits - bits.Count);
        for (int i = 0; i < terminator; i++)
            bits.Add(false);

        while (bits.Count % 8 != 0)
            bits.Add(false);

        var codewords = new byte[spec.DataCodewords];
        int byteCount = bits.Count / 8;
        for (int i = 0; i < byteCount; i++)
        {
            int val = 0;
            for (int b = 0; b < 8; b++)
                val = (val << 1) | (bits[i * 8 + b] ? 1 : 0);
            codewords[i] = (byte)val;
        }

        byte[] padBytes = { 0xEC, 0x11 };
        for (int i = byteCount; i < spec.DataCodewords; i++)
            codewords[i] = padBytes[(i - byteCount) % 2];

        return codewords;
    }

    private static byte[] BuildInterleavedCodewords(byte[] dataCodewords, VersionSpec spec)
    {
        var dataBlocks = new List<byte[]>(spec.Group1Blocks + spec.Group2Blocks);
        var ecBlocks = new List<byte[]>(spec.Group1Blocks + spec.Group2Blocks);

        int offset = 0;
        for (int i = 0; i < spec.Group1Blocks; i++)
        {
            var block = new byte[spec.Group1DataLen];
            Buffer.BlockCopy(dataCodewords, offset, block, 0, block.Length);
            offset += block.Length;
            dataBlocks.Add(block);
            ecBlocks.Add(ComputeReedSolomon(block, spec.EcPerBlock));
        }
        for (int i = 0; i < spec.Group2Blocks; i++)
        {
            var block = new byte[spec.Group2DataLen];
            Buffer.BlockCopy(dataCodewords, offset, block, 0, block.Length);
            offset += block.Length;
            dataBlocks.Add(block);
            ecBlocks.Add(ComputeReedSolomon(block, spec.EcPerBlock));
        }

        int totalCodewords = spec.DataCodewords + (dataBlocks.Count * spec.EcPerBlock);
        var result = new List<byte>(totalCodewords);
        int maxDataLen = dataBlocks.Max(b => b.Length);

        for (int i = 0; i < maxDataLen; i++)
        {
            foreach (var block in dataBlocks)
            {
                if (i < block.Length)
                    result.Add(block[i]);
            }
        }

        for (int i = 0; i < spec.EcPerBlock; i++)
        {
            foreach (var ec in ecBlocks)
                result.Add(ec[i]);
        }

        return result.ToArray();
    }

    private static void DrawFunctionModules(bool[,] modules, bool[,] isFunc, VersionSpec spec)
    {
        int size = modules.GetLength(0);

        void SetFunc(int r, int c, bool dark)
        {
            if ((uint)r >= (uint)size || (uint)c >= (uint)size)
                return;
            modules[r, c] = dark;
            isFunc[r, c] = true;
        }

        void DrawFinder(int topRow, int leftCol)
        {
            for (int r = -1; r <= 7; r++)
            {
                for (int c = -1; c <= 7; c++)
                {
                    int rr = topRow + r;
                    int cc = leftCol + c;
                    if ((uint)rr >= (uint)size || (uint)cc >= (uint)size)
                        continue;

                    int dist = Math.Max(Math.Abs(r - 3), Math.Abs(c - 3));
                    bool dark = dist != 2 && dist != 4;
                    SetFunc(rr, cc, dark);
                }
            }
        }

        DrawFinder(0, 0);
        DrawFinder(0, size - 7);
        DrawFinder(size - 7, 0);

        // Timing patterns
        for (int i = 8; i < size - 8; i++)
        {
            bool dark = (i % 2) == 0;
            SetFunc(6, i, dark);
            SetFunc(i, 6, dark);
        }

        // Alignment patterns
        var centers = spec.AlignmentCenters;
        for (int i = 0; i < centers.Length; i++)
        {
            for (int j = 0; j < centers.Length; j++)
            {
                int cr = centers[i];
                int cc = centers[j];
                bool nearTopLeft = cr <= 8 && cc <= 8;
                bool nearTopRight = cr <= 8 && cc >= size - 9;
                bool nearBottomLeft = cr >= size - 9 && cc <= 8;
                if (nearTopLeft || nearTopRight || nearBottomLeft)
                    continue;

                for (int dr = -2; dr <= 2; dr++)
                {
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        int dist = Math.Max(Math.Abs(dr), Math.Abs(dc));
                        SetFunc(cr + dr, cc + dc, dist != 1);
                    }
                }
            }
        }

        // Dark module
        SetFunc(4 * spec.Version + 9, 8, true);

        // Reserve format info cells
        for (int i = 0; i <= 8; i++)
        {
            if (i != 6)
            {
                SetFunc(8, i, false);
                SetFunc(i, 8, false);
            }
        }
        for (int i = 0; i < 8; i++)
            SetFunc(8, size - 8 + i, false);
        for (int i = 0; i < 7; i++)
            SetFunc(size - 7 + i, 8, false);

        // Reserve version info cells (v >= 7)
        if (spec.Version >= 7)
        {
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    SetFunc(i, size - 11 + j, false);
                    SetFunc(size - 11 + j, i, false);
                }
            }
        }
    }

    private static void PlaceDataBits(bool[,] modules, bool[,] isFunc, byte[] codewords)
    {
        int size = modules.GetLength(0);
        int totalBits = codewords.Length * 8;
        int bitIdx = 0;
        bool goingUp = true;

        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
                right = 5;

            for (int step = 0; step < size; step++)
            {
                int r = goingUp ? (size - 1 - step) : step;
                for (int dx = 0; dx < 2; dx++)
                {
                    int c = right - dx;
                    if (isFunc[r, c])
                        continue;

                    bool dark = false;
                    if (bitIdx < totalBits)
                    {
                        byte b = codewords[bitIdx >> 3];
                        dark = ((b >> (7 - (bitIdx & 7))) & 1) != 0;
                        bitIdx++;
                    }
                    modules[r, c] = dark;
                }
            }

            goingUp = !goingUp;
        }
    }

    private static void ApplyMask(bool[,] modules, bool[,] isFunc, int mask)
    {
        int size = modules.GetLength(0);
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (isFunc[r, c])
                    continue;

                bool invert = mask switch
                {
                    0 => (r + c) % 2 == 0,
                    1 => r % 2 == 0,
                    2 => c % 3 == 0,
                    3 => (r + c) % 3 == 0,
                    4 => ((r / 2) + (c / 3)) % 2 == 0,
                    5 => ((r * c) % 2) + ((r * c) % 3) == 0,
                    6 => (((r * c) % 2) + ((r * c) % 3)) % 2 == 0,
                    7 => (((r + c) % 2) + ((r * c) % 3)) % 2 == 0,
                    _ => false
                };

                if (invert)
                    modules[r, c] = !modules[r, c];
            }
        }
    }

    private static void DrawFormatInfo(bool[,] modules, int version, int mask)
    {
        int size = modules.GetLength(0);
        // ECC Level M = 00
        int data = mask & 0x7;
        int rem = data;
        for (int i = 0; i < 10; i++)
            rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        int bits = ((data << 10) | rem) ^ 0x5412;

        // Copy 1 around top-left finder
        for (int i = 0; i <= 5; i++)
            modules[8, i] = ((bits >> (14 - i)) & 1) != 0;
        modules[8, 7] = ((bits >> 8) & 1) != 0;
        modules[8, 8] = ((bits >> 7) & 1) != 0;
        modules[7, 8] = ((bits >> 6) & 1) != 0;
        for (int i = 0; i < 6; i++)
            modules[5 - i, 8] = ((bits >> (5 - i)) & 1) != 0;

        // Copy 2 bottom-left + top-right
        for (int i = 0; i < 7; i++)
            modules[size - 1 - i, 8] = ((bits >> (14 - i)) & 1) != 0;
        for (int i = 0; i < 8; i++)
            modules[8, size - 8 + i] = ((bits >> (7 - i)) & 1) != 0;

        // Re-assert dark module
        modules[4 * version + 9, 8] = true;
    }

    private static void DrawVersionInfo(bool[,] modules, int version)
    {
        int size = modules.GetLength(0);
        int rem = version;
        for (int i = 0; i < 12; i++)
            rem = (rem << 1) ^ (((rem >> 11) & 1) * 0x1F25);
        int bits = (version << 12) | rem;

        for (int i = 0; i < 18; i++)
        {
            bool dark = ((bits >> i) & 1) != 0;
            int a = size - 11 + (i % 3);
            int b = i / 3;
            modules[b, a] = dark;
            modules[a, b] = dark;
        }
    }

    private static int EvaluatePenalty(bool[,] m)
    {
        int size = m.GetLength(0);
        int penalty = 0;

        // N1: 5+ consecutive same color in rows and columns
        for (int r = 0; r < size; r++)
        {
            bool prevRow = m[r, 0];
            int runRow = 1;
            bool prevCol = m[0, r];
            int runCol = 1;

            for (int c = 1; c < size; c++)
            {
                if (m[r, c] == prevRow)
                {
                    runRow++;
                }
                else
                {
                    if (runRow >= 5)
                        penalty += 3 + (runRow - 5);
                    prevRow = m[r, c];
                    runRow = 1;
                }

                if (m[c, r] == prevCol)
                {
                    runCol++;
                }
                else
                {
                    if (runCol >= 5)
                        penalty += 3 + (runCol - 5);
                    prevCol = m[c, r];
                    runCol = 1;
                }
            }

            if (runRow >= 5)
                penalty += 3 + (runRow - 5);
            if (runCol >= 5)
                penalty += 3 + (runCol - 5);
        }

        // N2: 2x2 same-color blocks
        for (int r = 0; r < size - 1; r++)
        {
            for (int c = 0; c < size - 1; c++)
            {
                bool color = m[r, c];
                if (m[r, c + 1] == color && m[r + 1, c] == color && m[r + 1, c + 1] == color)
                    penalty += 3;
            }
        }

        // N3: finder-like 1:1:3:1:1 with 4 light modules on either side
        static bool MatchPattern(Func<int, bool> get, int start, bool forward)
        {
            // 10111010000 (forward) or 00001011101 (reverse)
            int[] pat = forward
                ? new[] { 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0 }
                : new[] { 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1 };
            for (int k = 0; k < 11; k++)
            {
                if ((get(start + k) ? 1 : 0) != pat[k])
                    return false;
            }
            return true;
        }

        for (int r = 0; r < size; r++)
        {
            int rr = r;
            for (int c = 0; c <= size - 11; c++)
            {
                if (MatchPattern(idx => m[rr, idx], c, true) || MatchPattern(idx => m[rr, idx], c, false))
                    penalty += 40;
                if (MatchPattern(idx => m[idx, rr], c, true) || MatchPattern(idx => m[idx, rr], c, false))
                    penalty += 40;
            }
        }

        // N4: dark ratio deviation from 50%
        int darkCount = 0;
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (m[r, c])
                    darkCount++;

        double percent = (darkCount * 100.0) / (size * size);
        int prevMultiple = (int)(Math.Floor(percent / 5.0) * 5.0);
        int nextMultiple = prevMultiple + 5;
        int k1 = Math.Abs(prevMultiple - 50) / 5;
        int k2 = Math.Abs(nextMultiple - 50) / 5;
        penalty += Math.Min(k1, k2) * 10;

        return penalty;
    }
}

internal static class ImageToolkitV380
{
    internal static (long BeforeBytes, long AfterBytes, int Width, int Height) OptimizeAndStripExif(
        string inputPath,
        string outputPath,
        int maxWidth = 0,
        int jpegQuality = 85,
        string? targetFormat = null)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            throw new FileNotFoundException("Input image file not found.", inputPath);
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output image path is required.", nameof(outputPath));

        long beforeBytes = new FileInfo(inputPath).Length;

        BitmapSource frame;
        using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("Image file contains no frames.");
            frame = decoder.Frames[0];
        }

        if (maxWidth > 0 && frame.PixelWidth > maxWidth)
        {
            double scale = (double)maxWidth / frame.PixelWidth;
            frame = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        }

        // Re-materialize raw pixels without metadata so all EXIF/GPS/camera tags are completely stripped.
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        int stride = w * 4;
        var rawPixels = new byte[h * stride];
        converted.CopyPixels(rawPixels, stride, 0);
        var cleanBitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, rawPixels, stride);
        cleanBitmap.Freeze();

        string format = (targetFormat ?? System.IO.Path.GetExtension(outputPath) ?? ".png")
            .Trim()
            .TrimStart('.')
            .ToLowerInvariant();

        BitmapEncoder encoder = format switch
        {
            "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
            "bmp" => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        encoder.Frames.Add(BitmapFrame.Create(cleanBitmap));

        string fullOut = System.IO.Path.GetFullPath(outputPath);
        string? outDir = System.IO.Path.GetDirectoryName(fullOut);
        if (!string.IsNullOrWhiteSpace(outDir))
            Directory.CreateDirectory(outDir);

        string tmp = fullOut + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(outFs);
            }
            File.Move(tmp, fullOut, true);
        }
        finally
        {
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch (IOException) { }
        }

        long afterBytes = new FileInfo(fullOut).Length;
        return (beforeBytes, afterBytes, w, h);
    }
}

internal static class HashToolkitV380
{
    private static readonly Regex Hex64Regex = new(@"\b[0-9a-fA-F]{64}\b", RegexOptions.Compiled);

    internal static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("File not found for hashing.", filePath);

        await using var fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65536,
            useAsync: true);
        byte[] digest = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    internal static bool VerifySha256(string actualHex, string expectedText)
    {
        if (string.IsNullOrWhiteSpace(actualHex) || string.IsNullOrWhiteSpace(expectedText))
            return false;

        string actual = actualHex.Trim().ToLowerInvariant();
        if (actual.Length != 64 || !actual.All(IsHexChar))
            return false;

        string candidate = "";
        string trimmed = expectedText.Trim();
        var match = Hex64Regex.Match(trimmed);
        if (match.Success)
        {
            candidate = match.Value.ToLowerInvariant();
        }
        else
        {
            string firstToken = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (firstToken.Length == 64 && firstToken.All(IsHexChar))
                candidate = firstToken.ToLowerInvariant();
        }

        if (candidate.Length != 64)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual),
            Encoding.ASCII.GetBytes(candidate));
    }

    private static bool IsHexChar(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

internal static class ZipToolkitV380
{
    internal static Task CreateZipAsync(
        IEnumerable<string> paths,
        string outputZipPath,
        CancellationToken ct = default)
    {
        if (paths == null)
            throw new ArgumentNullException(nameof(paths));
        if (string.IsNullOrWhiteSpace(outputZipPath))
            throw new ArgumentException("Output ZIP path is required.", nameof(outputZipPath));

        var sourceList = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
        if (sourceList.Count == 0)
            throw new ArgumentException("Select at least one file or folder to bundle.", nameof(paths));

        return Task.Run(() =>
        {
            string fullOut = System.IO.Path.GetFullPath(outputZipPath);
            string? outDir = System.IO.Path.GetDirectoryName(fullOut);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);

            string tmp = fullOut + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            static string SanitizeEntryName(string rawRelative)
            {
                var segments = rawRelative
                    .Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Where(seg => seg != "." && seg != ".." && !seg.Contains(':'))
                    .ToArray();
                return string.Join('/', segments);
            }

            string UniqueEntryName(string entryName)
            {
                if (usedNames.Add(entryName))
                    return entryName;
                string dir = System.IO.Path.GetDirectoryName(entryName)?.Replace('\\', '/') ?? "";
                string stem = System.IO.Path.GetFileNameWithoutExtension(entryName);
                string ext = System.IO.Path.GetExtension(entryName);
                int idx = 2;
                while (true)
                {
                    string candidate = string.IsNullOrEmpty(dir)
                        ? $"{stem}_{idx}{ext}"
                        : $"{dir}/{stem}_{idx}{ext}";
                    if (usedNames.Add(candidate))
                        return candidate;
                    idx++;
                }
            }

            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
                {
                    int addedCount = 0;
                    foreach (string rawPath in sourceList)
                    {
                        ct.ThrowIfCancellationRequested();
                        string full = System.IO.Path.GetFullPath(rawPath);

                        if (File.Exists(full))
                        {
                            if (string.Equals(full, fullOut, StringComparison.OrdinalIgnoreCase))
                                continue;
                            string safeName = SanitizeEntryName(System.IO.Path.GetFileName(full));
                            if (string.IsNullOrEmpty(safeName))
                                continue;
                            archive.CreateEntryFromFile(full, UniqueEntryName(safeName), CompressionLevel.Optimal);
                            addedCount++;
                        }
                        else if (Directory.Exists(full))
                        {
                            string rootName = new DirectoryInfo(full).Name;
                            foreach (string file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
                            {
                                ct.ThrowIfCancellationRequested();
                                if (string.Equals(file, fullOut, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                string rel = System.IO.Path.GetRelativePath(full, file);
                                string combined = SanitizeEntryName(rootName + "/" + rel);
                                if (string.IsNullOrEmpty(combined))
                                    continue;
                                archive.CreateEntryFromFile(file, UniqueEntryName(combined), CompressionLevel.Optimal);
                                addedCount++;
                            }
                        }
                    }

                    if (addedCount == 0)
                        throw new FileNotFoundException("No readable files were found in the selected paths.");
                }

                File.Move(tmp, fullOut, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (IOException) { }
            }
        }, ct);
    }
}

internal static class PdfToolkitV380
{
    internal static async Task<uint> GetPageCountAsync(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF path is required.", nameof(pdfPath));

        string full = System.IO.Path.GetFullPath(pdfPath);
        if (!File.Exists(full))
            throw new FileNotFoundException("PDF file not found.", full);

        await using var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        using var ras = fs.AsRandomAccessStream();
        var doc = await Windows.Data.Pdf.PdfDocument.LoadFromStreamAsync(ras).AsTask().ConfigureAwait(false);
        return doc.PageCount;
    }

    internal static async Task<int> ExportPagesAsPngAsync(
        string pdfPath,
        string outputFolder,
        int startPage1Based = 1,
        int maxPages = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF path is required.", nameof(pdfPath));
        if (string.IsNullOrWhiteSpace(outputFolder))
            throw new ArgumentException("Output folder is required.", nameof(outputFolder));
        if (startPage1Based < 1)
            throw new ArgumentOutOfRangeException(nameof(startPage1Based));
        if (maxPages < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPages));

        string full = System.IO.Path.GetFullPath(pdfPath);
        if (!File.Exists(full))
            throw new FileNotFoundException("PDF file not found.", full);

        Directory.CreateDirectory(outputFolder);

        await using var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        using var ras = fs.AsRandomAccessStream();
        var doc = await Windows.Data.Pdf.PdfDocument.LoadFromStreamAsync(ras).AsTask(ct).ConfigureAwait(false);

        uint total = doc.PageCount;
        if ((uint)startPage1Based > total)
            return 0;

        uint startIdx = (uint)(startPage1Based - 1);
        uint endExclusive = (uint)Math.Min((long)total, (long)startIdx + maxPages);
        string baseName = System.IO.Path.GetFileNameWithoutExtension(full);
        int exported = 0;

        for (uint i = startIdx; i < endExclusive; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var page = doc.GetPage(i);
            using var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var options = new Windows.Data.Pdf.PdfPageRenderOptions
            {
                BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                IsIgnoringHighContrast = true
            };
            await page.RenderToStreamAsync(memStream, options).AsTask(ct).ConfigureAwait(false);
            await memStream.FlushAsync().AsTask(ct).ConfigureAwait(false);
            memStream.Seek(0);

            string outFile = System.IO.Path.Combine(outputFolder, $"{baseName}_page_{i + 1:D3}.png");
            string tmp = outFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var outStream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                using (var reader = memStream.AsStreamForRead())
                {
                    await reader.CopyToAsync(outStream, ct).ConfigureAwait(false);
                }
                File.Move(tmp, outFile, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (IOException) { }
            }

            exported++;
        }

        return exported;
    }
}

internal static class DeskSpaceFileToolkitV380
{
    internal static void Show(Window? owner, IEnumerable<string>? initialFiles = null)
    {
        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Offline File Toolkit",
            940,
            660,
            owner,
            transient: false);
        win.MinWidth = 740;
        win.MinHeight = 520;

        var preloaded = initialFiles?
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim())
            .ToArray() ?? Array.Empty<string>();

        var tabs = new TabControl
        {
            Margin = new Thickness(14),
            Background = DeskSpaceDesignV283.Solid(p.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            ItemContainerStyle = DeskSpaceExcelProV380.BuildTabItemStyle(p)
        };

        tabs.Items.Add(BuildQrTab(p));
        tabs.Items.Add(BuildImageOptimizeTab(p, preloaded));
        tabs.Items.Add(BuildSha256Tab(p, preloaded));
        tabs.Items.Add(BuildPdfTab(p, preloaded));
        tabs.Items.Add(BuildZipTab(p, preloaded));

        win.Content = tabs;
        win.Show();
        win.Activate();
    }

    private static TabItem BuildQrTab(DeskSpaceDesignV283.Palette p)
    {
        var tab = new TabItem { Header = "QR Generator" };
        var root = new DockPanel { Margin = new Thickness(12) };

        var modeCombo = new WpfComboBox { ItemsSource = new[] { "Text / URL", "Wi-Fi Network" }, SelectedIndex = 0, Width = 170 };
        var payloadBox = new WpfTextBox
        {
            AcceptsReturn = true,
            Height = 90,
            Text = "https://localhost",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 8)
        };
        DeskSpaceDesignV283.Input(payloadBox, p);

        var ssidBox = new WpfTextBox { Margin = new Thickness(0, 4, 0, 6) };
        DeskSpaceDesignV283.Input(ssidBox, p);
        var passBox = new WpfTextBox { Margin = new Thickness(0, 4, 0, 6) };
        DeskSpaceDesignV283.Input(passBox, p);
        var secCombo = new WpfComboBox { ItemsSource = new[] { "WPA", "WEP", "nopass" }, SelectedIndex = 0, Margin = new Thickness(0, 4, 0, 8) };

        var wifiPanel = new StackPanel { Visibility = Visibility.Collapsed };
        wifiPanel.Children.Add(new TextBlock { Text = "SSID", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        wifiPanel.Children.Add(ssidBox);
        wifiPanel.Children.Add(new TextBlock { Text = "Password", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        wifiPanel.Children.Add(passBox);
        wifiPanel.Children.Add(new TextBlock { Text = "Encryption", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        wifiPanel.Children.Add(secCombo);

        var qrImage = new WpfImage
        {
            Width = 260,
            Height = 260,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(qrImage, BitmapScalingMode.NearestNeighbor);

        var status = new TextBlock
        {
            Text = "100% offline ISO/IEC 18004 QR encoder (Versions 1–10, ECC-M, up to 213 UTF-8 bytes).",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        WriteableBitmap? currentBmp = null;

        string CurrentPayload()
        {
            if (modeCombo.SelectedIndex == 1)
            {
                static string EscWifi(string v) => v.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace(":", "\\:");
                string ssid = EscWifi(ssidBox.Text);
                string pass = EscWifi(passBox.Text);
                string sec = secCombo.SelectedItem as string ?? "WPA";
                return sec == "nopass" ? $"WIFI:T:nopass;S:{ssid};;" : $"WIFI:T:{sec};S:{ssid};P:{pass};;";
            }
            return payloadBox.Text;
        }

        void RenderQr()
        {
            try
            {
                string data = CurrentPayload();
                currentBmp = QrCodeEncoderV380.RenderBitmap(data, 8, 4);
                qrImage.Source = currentBmp;
                int bytes = Encoding.UTF8.GetByteCount(data);
                status.Text = $"Rendered offline QR code ({bytes}/213 UTF-8 bytes, {currentBmp.PixelWidth}×{currentBmp.PixelHeight} px).";
            }
            catch (Exception ex)
            {
                currentBmp = null;
                qrImage.Source = null;
                status.Text = ex.Message;
            }
        }

        modeCombo.SelectionChanged += (_, _) =>
        {
            bool wifi = modeCombo.SelectedIndex == 1;
            wifiPanel.Visibility = wifi ? Visibility.Visible : Visibility.Collapsed;
            payloadBox.Visibility = wifi ? Visibility.Collapsed : Visibility.Visible;
            RenderQr();
        };
        payloadBox.TextChanged += (_, _) => RenderQr();
        ssidBox.TextChanged += (_, _) => RenderQr();
        passBox.TextChanged += (_, _) => RenderQr();
        secCombo.SelectionChanged += (_, _) => RenderQr();

        var left = new StackPanel { Width = 380, Margin = new Thickness(0, 0, 14, 0) };
        left.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("QR Content", p));
        left.Children.Add(modeCombo);
        left.Children.Add(payloadBox);
        left.Children.Add(wifiPanel);

        var btns = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var saveBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save PNG...", () =>
        {
            if (currentBmp == null)
                return;
            var dlg = new SaveFileDialog
            {
                Title = "Save QR Code PNG",
                FileName = "deskspace-qr.png",
                Filter = "PNG Image (*.png)|*.png",
                DefaultExt = ".png"
            };
            if (dlg.ShowDialog() == true)
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(currentBmp));
                using var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
                enc.Save(fs);
                status.Text = $"Saved QR code to {dlg.FileName}.";
            }
        }, true, "Export crisp PNG");
        saveBtn.Margin = new Thickness(0, 0, 8, 0);

        var copyBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Image", () =>
        {
            if (currentBmp == null)
                return;
            System.Windows.Clipboard.SetImage(currentBmp);
            status.Text = "Copied QR image to clipboard.";
        }, false, "Copy QR bitmap to clipboard");

        btns.Children.Add(saveBtn);
        btns.Children.Add(copyBtn);
        left.Children.Add(btns);

        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);

        var previewCard = DeskSpaceFluentBackdropV380.CreateCard(
            new Border
            {
                Background = WpfBrushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = qrImage
            },
            p);
        root.Children.Add(previewCard);

        RenderQr();
        tab.Content = root;
        return tab;
    }

    private static TabItem BuildImageOptimizeTab(DeskSpaceDesignV283.Palette p, string[] preloaded)
    {
        var tab = new TabItem { Header = "Image Optimize & Strip EXIF" };
        var stack = new StackPanel { Margin = new Thickness(12), AllowDrop = true };

        var fileList = new ListBox { Height = 150, Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Selection(fileList, p);
        foreach (var f in preloaded.Where(IsImageExt))
            fileList.Items.Add(f);

        stack.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        stack.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var f in files.Where(IsImageExt))
                    if (!fileList.Items.Contains(f))
                        fileList.Items.Add(f);
            }
        };

        var maxWBox = new WpfTextBox { Width = 90, Text = "0", ToolTip = "0 keeps original width" };
        DeskSpaceDesignV283.Input(maxWBox, p);
        var qualBox = new WpfTextBox { Width = 70, Text = "85" };
        DeskSpaceDesignV283.Input(qualBox, p);
        var fmtCombo = new WpfComboBox { ItemsSource = new[] { "jpeg", "png", "bmp" }, SelectedIndex = 0, Width = 90 };

        var status = new TextBlock
        {
            Text = "Drop images here or click Add Images. Re-encodes pixel buffers with zero EXIF/GPS metadata.",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var row = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var addBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Add Images...", () =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (string f in dlg.FileNames)
                    if (!fileList.Items.Contains(f))
                        fileList.Items.Add(f);
            }
        }, false, "Select image files");
        addBtn.Margin = new Thickness(0, 0, 8, 4);

        var runBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Optimize & Strip EXIF", () =>
        {
            if (fileList.Items.Count == 0)
            {
                status.Text = "Add at least one image first.";
                return;
            }
            int maxW = int.TryParse(maxWBox.Text.Trim(), out int mw) && mw > 0 ? mw : 0;
            int qual = int.TryParse(qualBox.Text.Trim(), out int q) ? Math.Clamp(q, 1, 100) : 85;
            string fmt = fmtCombo.SelectedItem as string ?? "jpeg";
            string ext = fmt == "jpeg" ? ".jpg" : "." + fmt;

            long totalBefore = 0, totalAfter = 0;
            int count = 0;
            try
            {
                foreach (string src in fileList.Items.OfType<string>())
                {
                    string dir = System.IO.Path.GetDirectoryName(src) ?? WorkspaceStore.Root;
                    string stem = System.IO.Path.GetFileNameWithoutExtension(src);
                    string outPath = System.IO.Path.Combine(dir, $"{stem}_clean{ext}");
                    var res = ImageToolkitV380.OptimizeAndStripExif(src, outPath, maxW, qual, fmt);
                    totalBefore += res.BeforeBytes;
                    totalAfter += res.AfterBytes;
                    count++;
                }
                status.Text = $"Processed {count} image(s) (EXIF stripped). {totalBefore / 1024.0:F1} KB → {totalAfter / 1024.0:F1} KB.";
            }
            catch (Exception ex)
            {
                status.Text = "Image optimize error: " + ex.Message;
            }
        }, true, "Write clean copies beside source files");
        runBtn.Margin = new Thickness(0, 0, 8, 4);

        row.Children.Add(addBtn);
        row.Children.Add(runBtn);
        row.Children.Add(new TextBlock { Text = "Max Width:", Foreground = DeskSpaceDesignV283.Solid(p.Text), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });
        row.Children.Add(maxWBox);
        row.Children.Add(new TextBlock { Text = "JPEG Quality:", Foreground = DeskSpaceDesignV283.Solid(p.Text), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
        row.Children.Add(qualBox);
        row.Children.Add(new TextBlock { Text = "Format:", Foreground = DeskSpaceDesignV283.Solid(p.Text), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
        row.Children.Add(fmtCombo);

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Images (Drag & Drop supported)", p));
        stack.Children.Add(fileList);
        stack.Children.Add(row);
        stack.Children.Add(status);

        tab.Content = DeskSpaceFluentBackdropV380.CreateCard(stack, p);
        return tab;
    }

    private static TabItem BuildSha256Tab(DeskSpaceDesignV283.Palette p, string[] preloaded)
    {
        var tab = new TabItem { Header = "SHA-256 Hash & Verify" };
        var stack = new StackPanel { Margin = new Thickness(12) };

        var pathBox = new WpfTextBox { Text = preloaded.FirstOrDefault(File.Exists) ?? "", Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Input(pathBox, p);
        var actualBox = new WpfTextBox { IsReadOnly = true, Margin = new Thickness(0, 0, 0, 8), FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI") };
        DeskSpaceDesignV283.Input(actualBox, p);
        var expectedBox = new WpfTextBox { Margin = new Thickness(0, 0, 0, 8), FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI") };
        DeskSpaceDesignV283.Input(expectedBox, p);
        var verdict = new TextBlock { Text = "Select a file and compute SHA-256.", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 13 };

        void UpdateVerdict()
        {
            if (string.IsNullOrWhiteSpace(actualBox.Text))
            {
                verdict.Text = "Compute SHA-256 first.";
                verdict.Foreground = DeskSpaceDesignV283.Solid(p.Muted);
                return;
            }
            if (string.IsNullOrWhiteSpace(expectedBox.Text))
            {
                verdict.Text = "SHA-256 computed. Paste an expected hash to verify.";
                verdict.Foreground = DeskSpaceDesignV283.Solid(p.Text);
                return;
            }
            bool match = HashToolkitV380.VerifySha256(actualBox.Text, expectedBox.Text);
            verdict.Text = match ? "MATCH — SHA-256 digest verified." : "MISMATCH — Digest does not match expected hash.";
            verdict.Foreground = DeskSpaceDesignV283.Solid(match ? DeskSpaceDesignV283.Success(p) : DeskSpaceDesignV283.Danger(p));
        }

        expectedBox.TextChanged += (_, _) => UpdateVerdict();

        var btns = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var browseBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Browse...", () =>
        {
            var dlg = new OpenFileDialog { CheckFileExists = true };
            if (dlg.ShowDialog() == true)
                pathBox.Text = dlg.FileName;
        }, false, "Select file");
        browseBtn.Margin = new Thickness(0, 0, 8, 0);

        var computeBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Compute SHA-256", async () =>
        {
            try
            {
                verdict.Text = "Computing SHA-256...";
                actualBox.Text = await HashToolkitV380.ComputeSha256Async(pathBox.Text.Trim());
                UpdateVerdict();
            }
            catch (Exception ex)
            {
                verdict.Text = ex.Message;
                verdict.Foreground = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.Danger(p));
            }
        }, true, "Compute lowercase SHA-256 hex");

        btns.Children.Add(browseBtn);
        btns.Children.Add(computeBtn);

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("File Path", p));
        stack.Children.Add(pathBox);
        stack.Children.Add(btns);
        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Computed SHA-256", p));
        stack.Children.Add(actualBox);
        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Expected SHA-256 (bare hex or '<hash>  filename')", p));
        stack.Children.Add(expectedBox);
        stack.Children.Add(verdict);

        tab.Content = DeskSpaceFluentBackdropV380.CreateCard(stack, p);
        return tab;
    }

    private static TabItem BuildPdfTab(DeskSpaceDesignV283.Palette p, string[] preloaded)
    {
        var tab = new TabItem { Header = "PDF Pages to PNG" };
        var stack = new StackPanel { Margin = new Thickness(12) };

        string? initialPdf = preloaded.FirstOrDefault(f =>
            File.Exists(f) && System.IO.Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));

        var pdfBox = new WpfTextBox { Text = initialPdf ?? "", Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Input(pdfBox, p);
        var outDirBox = new WpfTextBox
        {
            Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "DeskSpacePdfExport"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        DeskSpaceDesignV283.Input(outDirBox, p);

        var startBox = new WpfTextBox { Width = 70, Text = "1" };
        DeskSpaceDesignV283.Input(startBox, p);
        var maxBox = new WpfTextBox { Width = 70, Text = "20" };
        DeskSpaceDesignV283.Input(maxBox, p);
        var status = new TextBlock
        {
            Text = "Uses built-in Windows.Data.Pdf WinRT renderer (100% offline).",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var row = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        var browseBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Browse PDF...", () =>
        {
            var dlg = new OpenFileDialog { Filter = "PDF Documents (*.pdf)|*.pdf", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
                pdfBox.Text = dlg.FileName;
        }, false, "Choose PDF file");
        browseBtn.Margin = new Thickness(0, 0, 8, 4);

        var countBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Check Page Count", async () =>
        {
            try
            {
                uint pages = await PdfToolkitV380.GetPageCountAsync(pdfBox.Text.Trim());
                status.Text = $"PDF contains {pages} page(s).";
            }
            catch (Exception ex)
            {
                status.Text = "PDF error: " + ex.Message;
            }
        }, false, "Inspect PDF page count");
        countBtn.Margin = new Thickness(0, 0, 8, 4);

        var exportBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Export PNGs", async () =>
        {
            try
            {
                int start = int.TryParse(startBox.Text.Trim(), out int s) && s >= 1 ? s : 1;
                int max = int.TryParse(maxBox.Text.Trim(), out int m) && m >= 1 ? m : 20;
                status.Text = "Rendering PDF pages...";
                int count = await PdfToolkitV380.ExportPagesAsPngAsync(pdfBox.Text.Trim(), outDirBox.Text.Trim(), start, max);
                status.Text = $"Exported {count} PNG page(s) to {outDirBox.Text.Trim()}.";
            }
            catch (Exception ex)
            {
                status.Text = "PDF export failed: " + ex.Message;
            }
        }, true, "Export selected page range as PNG");
        exportBtn.Margin = new Thickness(0, 0, 8, 4);

        row.Children.Add(browseBtn);
        row.Children.Add(countBtn);
        row.Children.Add(exportBtn);
        row.Children.Add(new TextBlock { Text = "Start page:", Foreground = DeskSpaceDesignV283.Solid(p.Text), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0) });
        row.Children.Add(startBox);
        row.Children.Add(new TextBlock { Text = "Max pages:", Foreground = DeskSpaceDesignV283.Solid(p.Text), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
        row.Children.Add(maxBox);

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("PDF File Path", p));
        stack.Children.Add(pdfBox);
        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Output Folder", p));
        stack.Children.Add(outDirBox);
        stack.Children.Add(row);
        stack.Children.Add(status);

        tab.Content = DeskSpaceFluentBackdropV380.CreateCard(stack, p);
        return tab;
    }

    private static TabItem BuildZipTab(DeskSpaceDesignV283.Palette p, string[] preloaded)
    {
        var tab = new TabItem { Header = "Bundle ZIP" };
        var stack = new StackPanel { Margin = new Thickness(12), AllowDrop = true };

        var itemsList = new ListBox { Height = 160, Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Selection(itemsList, p);
        foreach (var path in preloaded.Where(x => File.Exists(x) || Directory.Exists(x)))
            itemsList.Items.Add(path);

        stack.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        stack.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped)
            {
                foreach (string d in dropped)
                    if (!itemsList.Items.Contains(d))
                        itemsList.Items.Add(d);
            }
        };

        var outZipBox = new WpfTextBox
        {
            Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", $"DeskSpace_Bundle_{DateTime.Now:yyyyMMdd_HHmm}.zip"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        DeskSpaceDesignV283.Input(outZipBox, p);

        var status = new TextBlock
        {
            Text = "Drop files/folders here or browse to create a safe ZIP archive.",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var btns = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        var addFilesBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Add Files...", () =>
        {
            var dlg = new OpenFileDialog { Multiselect = true };
            if (dlg.ShowDialog() == true)
                foreach (string f in dlg.FileNames)
                    if (!itemsList.Items.Contains(f))
                        itemsList.Items.Add(f);
        }, false, "Add files to archive");
        addFilesBtn.Margin = new Thickness(0, 0, 8, 4);

        var addFolderBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Add Folder...", () =>
        {
            using var dlg = new Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                if (!itemsList.Items.Contains(dlg.SelectedPath))
                    itemsList.Items.Add(dlg.SelectedPath);
            }
        }, false, "Add entire directory");
        addFolderBtn.Margin = new Thickness(0, 0, 8, 4);

        var zipBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Create ZIP", async () =>
        {
            try
            {
                var list = itemsList.Items.OfType<string>().ToArray();
                status.Text = "Bundling ZIP archive...";
                await ZipToolkitV380.CreateZipAsync(list, outZipBox.Text.Trim());
                long bytes = new FileInfo(outZipBox.Text.Trim()).Length;
                status.Text = $"Created ZIP ({bytes / 1024.0:F1} KB): {outZipBox.Text.Trim()}";
            }
            catch (Exception ex)
            {
                status.Text = "ZIP failed: " + ex.Message;
            }
        }, true, "Create atomic ZIP archive");
        zipBtn.Margin = new Thickness(0, 0, 8, 4);

        btns.Children.Add(addFilesBtn);
        btns.Children.Add(addFolderBtn);
        btns.Children.Add(zipBtn);

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Files & Folders to Bundle", p));
        stack.Children.Add(itemsList);
        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Output ZIP Path", p));
        stack.Children.Add(outZipBox);
        stack.Children.Add(btns);
        stack.Children.Add(status);

        tab.Content = DeskSpaceFluentBackdropV380.CreateCard(stack, p);
        return tab;
    }

    private static bool IsImageExt(string path)
    {
        if (!File.Exists(path))
            return false;
        string ext = System.IO.Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
