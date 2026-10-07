using System.Globalization;
using System.Text;

namespace ImageInfoLab;

public sealed class ImageInfo
{
    public string FullPath = "", Name = "", Format = "?", Compression = "—", Status = "OK", Extra = "";
    public int Width, Height, Bpp;
    public double DpiX, DpiY;         
    public long FileSize;

    public string SizeText => Width > 0 ? $"{Width} × {Height}" : "—";
    public string DepthText => Bpp > 0 ? Bpp.ToString() : "—";
    public string DpiText => DpiX <= 0 ? "—"
        : (DpiY <= 0 || Math.Abs(DpiX - DpiY) < 0.01) ? F(DpiX) : $"{F(DpiX)} × {F(DpiY)}";
    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}

static class Bin
{
    public static byte[] Take(Stream s, int n)
    {
        var b = new byte[n]; int o = 0;
        while (o < n)
        {
            int r = s.Read(b, o, n - o);
            if (r <= 0) throw new EndOfStreamException("неожиданный конец файла");
            o += r;
        }
        return b;
    }
    public static int LE16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
    public static uint LE32(byte[] b, int o) => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    public static int BE16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
    public static uint BE32(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
}

public static class HeaderReader
{
    public static ImageInfo Read(string path)
    {
        var info = new ImageInfo { FullPath = path, Name = Path.GetFileName(path) };
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096);
            info.FileSize = fs.Length;


            var sig = new byte[16]; int n = 0, r;
            while (n < 16 && (r = fs.Read(sig, n, 16 - n)) > 0) n += r;
            string fmt = Detect(sig, n);
            if (fmt == null) { info.Status = "Неизвестный формат"; return info; }
            info.Format = fmt;
            fs.Position = 0;
            switch (fmt)
            {
                case "PNG": Png(fs, info); break;
                case "JPEG": Jpeg(fs, info); break;
                case "GIF": Gif(fs, info); break;
                case "BMP": Bmp(fs, info); break;
                case "TIFF": Tiff(fs, info); break;
                case "PCX": Pcx(fs, info); break;
            }
        }
        catch (Exception ex) { info.Status = "Ошибка чтения: " + ex.Message; }   
        return info;
    }

    static string Detect(byte[] s, int n)
    {
        if (n >= 8 && s[0] == 0x89 && s[1] == 0x50 && s[2] == 0x4E && s[3] == 0x47 && s[4] == 0x0D && s[5] == 0x0A && s[6] == 0x1A && s[7] == 0x0A) return "PNG";
        if (n >= 3 && s[0] == 0xFF && s[1] == 0xD8 && s[2] == 0xFF) return "JPEG";
        if (n >= 6 && s[0] == 'G' && s[1] == 'I' && s[2] == 'F' && s[3] == '8' && (s[4] == '7' || s[4] == '9') && s[5] == 'a') return "GIF";
        if (n >= 2 && s[0] == 'B' && s[1] == 'M') return "BMP";
        if (n >= 4 && ((s[0] == 'I' && s[1] == 'I' && s[2] == 42 && s[3] == 0) || (s[0] == 'M' && s[1] == 'M' && s[2] == 0 && s[3] == 42))) return "TIFF";
        if (n >= 4 && s[0] == 0x0A && s[1] is 0 or 2 or 3 or 4 or 5 && s[2] is 0 or 1 && s[3] is 1 or 2 or 4 or 8) return "PCX";
        return null;
    }

    static void Png(FileStream fs, ImageInfo i)
    {
        fs.Position = 8;                                  
        while (fs.Position + 8 <= fs.Length)
        {
            var h = Bin.Take(fs, 8);                       
            long len = Bin.BE32(h, 0);
            string type = Encoding.ASCII.GetString(h, 4, 4);
            if (type == "IHDR")
            {
                var d = Bin.Take(fs, 13);
                i.Width = (int)Bin.BE32(d, 0); i.Height = (int)Bin.BE32(d, 4);
                int ch = d[9] switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 1 };
                i.Bpp = d[8] * ch;                         
                i.Compression = d[10] == 0 ? "Deflate" : "неизвестно";
                i.Extra = $"Метод фильтрации: {d[11]} (0 — адаптивная: None/Sub/Up/Average/Paeth){Environment.NewLine}" +
                          $"Интерлейс: {(d[12] == 0 ? "нет" : "Adam7")}";
                fs.Position += len - 13 + 4;
            }
            else if (type == "pHYs" && len == 9)
            {
                var d = Bin.Take(fs, 9);
                if (d[8] == 1) { i.DpiX = Bin.BE32(d, 0) * 0.0254; i.DpiY = Bin.BE32(d, 4) * 0.0254; }  
                fs.Position += 4;
            }
            else if (type == "IDAT" || type == "IEND") break;   
            else fs.Position += len + 4;                        
        }
        if (i.Width == 0) throw new InvalidDataException("не найден чанк IHDR");
    }

    static void Jpeg(FileStream fs, ImageInfo i)
    {
        fs.Position = 2;                                  

        while (fs.Position + 4 <= fs.Length)
        {
            if (fs.ReadByte() != 0xFF) throw new InvalidDataException("ожидался маркер JPEG");
            int m; do { m = fs.ReadByte(); } while (m == 0xFF);
            if (m < 0) break;
            if (m == 0 || m == 1 || (m >= 0xD0 && m <= 0xD8)) continue;   
            if (m == 0xD9 || m == 0xDA) break;                            
            int len = Bin.BE16(Bin.Take(fs, 2), 0) - 2;
            if (len < 0) throw new InvalidDataException("неверная длина сегмента");

            if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC)   
            {
                if (len < 6) throw new InvalidDataException("слишком короткий SOF");
                var d = Bin.Take(fs, 6);
                i.Height = Bin.BE16(d, 1); i.Width = Bin.BE16(d, 3); i.Bpp = d[0] * d[5];   
                i.Compression = "JPEG: " + m switch
                {
                    0xC0 => "Baseline DCT", 0xC1 => "Extended sequential", 0xC2 => "Progressive DCT",
                    0xC3 => "Lossless", _ => $"SOF{m - 0xC0}"
                };
                return;
            }
            if (m == 0xE0 && len >= 14)                   
            {
                var d = Bin.Take(fs, len);
                if (d[0] == 'J' && d[1] == 'F' && d[2] == 'I' && d[3] == 'F')
                {
                    int xd = Bin.BE16(d, 8), yd = Bin.BE16(d, 10);
                    if (d[7] == 1) { i.DpiX = xd; i.DpiY = yd; }                   
                    else if (d[7] == 2) { i.DpiX = xd * 2.54; i.DpiY = yd * 2.54; } 
                }
            }
            else fs.Position += len;                      
        }
        throw new InvalidDataException("не найден сегмент SOF");
    }

    static void Gif(FileStream fs, ImageInfo i)
    {
        var h = Bin.Take(fs, 13);                          
        i.Width = Bin.LE16(h, 6); i.Height = Bin.LE16(h, 8);
        int packed = h[10];
        bool gct = (packed & 0x80) != 0;
        int bits = (packed & 7) + 1;
        i.Bpp = gct ? bits : ((packed >> 4) & 7) + 1;    
        i.Compression = "LZW";
        i.Extra = (gct ? $"Глобальная палитра: {1 << bits} цветов" : "Глобальной палитры нет") +
                  Environment.NewLine + "Разрешение (dpi) в GIF не хранится";
    }

    static void Bmp(FileStream fs, ImageInfo i)
    {
        var h = Bin.Take(fs, 18);                         
        uint dib = Bin.LE32(h, 14);
        uint comp = 0, clrUsed = 0; int bpp;
        if (dib == 12)                                    
        {
            var d = Bin.Take(fs, 8);
            i.Width = Bin.LE16(d, 0); i.Height = Bin.LE16(d, 2); bpp = Bin.LE16(d, 6);
        }
        else                                             
        {
            var d = Bin.Take(fs, 36);
            i.Width = (int)Bin.LE32(d, 0); i.Height = Math.Abs((int)Bin.LE32(d, 4)); bpp = Bin.LE16(d, 10);
            comp = Bin.LE32(d, 12); clrUsed = Bin.LE32(d, 28);
            i.DpiX = (int)Bin.LE32(d, 20) * 0.0254; i.DpiY = (int)Bin.LE32(d, 24) * 0.0254;  
        }
        i.Bpp = bpp;
        i.Compression = comp switch
        {
            0 => "BI_RGB (без сжатия)", 1 => "BI_RLE8", 2 => "BI_RLE4", 3 => "BI_BITFIELDS", _ => $"код {comp}"
        };
        if (bpp <= 8)
            i.Extra = $"Палитра: {(clrUsed != 0 ? clrUsed : 1u << bpp)} цветов (записи по 4 байта: B, G, R, резерв)";
    }

    static string TiffComp(long c) => c switch
    {
        1 => "Без сжатия", 2 => "CCITT RLE", 3 => "CCITT Group 3", 4 => "CCITT Group 4", 5 => "LZW",
        6 or 7 => "JPEG", 8 or 32946 => "Deflate", 32773 => "PackBits", _ => $"код {c}"
    };

    static void Tiff(FileStream fs, ImageInfo i)
    {
        var h = Bin.Take(fs, 8);
        bool le = h[0] == 'I';                           
        int U16(byte[] b, int o) => le ? b[o] | (b[o + 1] << 8) : (b[o] << 8) | b[o + 1];
        uint U32(byte[] b, int o) => le
            ? (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24))
            : (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        double Rational(uint off)
        {
            fs.Position = off; var b = Bin.Take(fs, 8);
            uint num = U32(b, 0), den = U32(b, 4);
            return den == 0 ? 0 : (double)num / den;
        }

        fs.Position = U32(h, 4);                          
        int n = U16(Bin.Take(fs, 2), 0);
        var buf = Bin.Take(fs, n * 12);                   
        long spp = 1, comp = 1, unit = 2; long[] bits = { 1 }; double xr = 0, yr = 0;
        for (int k = 0; k < n; k++)
        {
            int o = k * 12, tag = U16(buf, o), type = U16(buf, o + 2);
            uint count = U32(buf, o + 4);
            long v = type == 3 ? U16(buf, o + 8) : U32(buf, o + 8);  
            switch (tag)
            {
                case 256: i.Width = (int)v; break;
                case 257: i.Height = (int)v; break;
                case 258:
                    if (count == 1) bits = new[] { v };
                    else if (count <= 16)
                    {
                        int c = (int)count; long[] r = new long[c];
                        byte[] raw = count * 2 <= 4 ? buf : null; int p = o + 8;
                        if (raw == null) { fs.Position = U32(buf, o + 8); raw = Bin.Take(fs, c * 2); p = 0; }
                        for (int q = 0; q < c; q++) r[q] = U16(raw, p + 2 * q);
                        bits = r;
                    }
                    break;
                case 259: comp = v; break;
                case 277: spp = v; break;
                case 282: xr = Rational(U32(buf, o + 8)); break;
                case 283: yr = Rational(U32(buf, o + 8)); break;
                case 296: unit = v; break;
            }
        }
        i.Bpp = bits.Length == 1 && spp > 1 ? (int)(bits[0] * spp) : (int)bits.Sum();
        i.Compression = TiffComp(comp);
        if (unit != 1) { double kf = unit == 3 ? 2.54 : 1; i.DpiX = xr * kf; i.DpiY = yr * kf; }   
        if (i.Width == 0 || i.Height == 0) throw new InvalidDataException("в IFD нет размеров изображения");
    }

    static void Pcx(FileStream fs, ImageInfo i)
    {
        var h = Bin.Take(fs, 128);                        
        i.Width = Bin.LE16(h, 8) - Bin.LE16(h, 4) + 1;    
        i.Height = Bin.LE16(h, 10) - Bin.LE16(h, 6) + 1;   
        i.Bpp = h[3] * h[65];                             
        i.DpiX = Bin.LE16(h, 12); i.DpiY = Bin.LE16(h, 14);
        i.Compression = h[2] == 1 ? "RLE (PCX)" : "Без сжатия";
        if (i.Bpp <= 4) i.Extra = $"Палитра: {1 << i.Bpp} цветов в заголовке";
        else if (i.Bpp == 8) i.Extra = "Палитра: 256 цветов в конце файла (маркер 0x0C + 768 байт)";
    }
}
