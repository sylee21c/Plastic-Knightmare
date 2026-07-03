using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class RemoveGreenBackground
{
    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: RemoveGreenBackground <input> <output>");
            return 2;
        }

        using Bitmap source = new Bitmap(args[0]);
        using Bitmap output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                bool isGreenKey = pixel.G > 150 && pixel.R < 90 && pixel.B < 90 && pixel.G > pixel.R * 1.8f && pixel.G > pixel.B * 1.8f;
                output.SetPixel(x, y, isGreenKey ? Color.FromArgb(0, pixel.R, pixel.G, pixel.B) : pixel);
            }
        }

        output.Save(args[1], ImageFormat.Png);
        return 0;
    }
}
