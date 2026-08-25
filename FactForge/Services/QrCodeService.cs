using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Avalonia.Media.Imaging;
using QRCoder;
using SkiaSharp;

namespace FactForge.Services;

public class QrCodeService
{
    public string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        throw new InvalidOperationException("No IPv4 address found for this machine.");
    }

    public Bitmap GenerateQrCode(string content, int pixelsPerModule = 16)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

        var modules = data.ModuleMatrix.Count;
        var size = modules * pixelsPerModule;

        using var skBitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(skBitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Fill };
        for (var y = 0; y < modules; y++)
        {
            for (var x = 0; x < modules; x++)
            {
                if (!data.ModuleMatrix[y][x]) continue;
                canvas.DrawRect(new SKRect(
                    x * pixelsPerModule, y * pixelsPerModule,
                    (x + 1) * pixelsPerModule, (y + 1) * pixelsPerModule), paint);
            }
        }

        using var ms = new MemoryStream();
        skBitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;
        return new Bitmap(ms);
    }
}
