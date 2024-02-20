using System;
using System.Drawing;
using System.IO;
using Hjg.Pngcs;
using System.Text;
using System.Drawing.Imaging;
using IndexedGTX;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Data;
using Hjg.Pngcs.Chunks;
using System.Reflection.PortableExecutable;

namespace IndexedGTX
{
    enum GXTFormat
    {
        None,
        Indexed,
        Grayscale,
        DXT
    }
    enum TextureType
    {
        Swizzled,
        Linear
    }
    
    class Programm
    {
        private static GXTFormat GXTFormat { get; set; } = GXTFormat.None;
        private static TextureType TextureType { get; set; } = TextureType.Swizzled;
        private static bool WithExt { get; set; } = false;
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                //args = new string[] { "-linear","-grey",@"ID00013.png" };
                args = new string[] { @"-linear", "-grey", "-ext", @"D:\Common\Oregairu\vita_zoku\system\font" };
                //args = new string[] { @"ID00030.png" };
                //args = new string[] { @"ID00030.gxt" };
            }
            var tasks = new List<Task>();
            foreach (var arg in args)
            {

                if (arg == "-grey")
                {
                    GXTFormat = GXTFormat.Grayscale;
                }
                else if (arg == "-linear")
                {
                    TextureType = TextureType.Linear;
                }
                else if (arg == "-ext")
                {
                    WithExt = true;
                    Console.WriteLine("WithExt");
                }
                else if (arg == "-dxt")
                {
                    GXTFormat = GXTFormat.DXT;
                }
                else
                {
                    var task = Task.Run(() => HandleFile(arg));
                    tasks.Add(task);
                }
            }
            Task.WaitAll(tasks.ToArray());
        }
        private static void HandleFile(string filename)
        {
            if (Directory.Exists(filename))
            {
                foreach(var file in Directory.GetFiles(filename,"*.png",SearchOption.AllDirectories)) 
                {
                    HandleFile(file);
                }
                return;
            }
            Console.WriteLine(filename);

            using var reader = new BinaryReader(File.OpenRead(filename));

            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            reader.BaseStream.Position = 0;


            if (magic == "GXT\0")
            {
                ConvertGXT(reader,Path.ChangeExtension(filename,".png"));
            }
            else if (magic.EndsWith("PNG"))
            {
                reader.BaseStream.Position = 0;
                var pngFile = new PngReader(reader.BaseStream);

                string output;
                if (WithExt)
                {
                    output = Path.ChangeExtension(filename, "gxt");
                }
                else
                {
                    output = Path.ChangeExtension(filename, "");
                }
                var info = pngFile.ImgInfo;
                if(GXTFormat == GXTFormat.None)
                {
                    if (info.BitDepth == 8 && info.Indexed)
                    {
                        ConvertIndexedPNG(reader.BaseStream, pngFile, output);
                    }
                    else if (info.BitDepth == 8 && info.Greyscale)
                    {
                        ConvertGreyScalePNG(reader.BaseStream, pngFile, output);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else if (GXTFormat == GXTFormat.Indexed)
                {
                    if (info.BitDepth != 8)
                    {
                        throw new ArgumentException();
                    }
                    ConvertIndexedPNG(reader.BaseStream,pngFile, output);
                }
                else if(GXTFormat == GXTFormat.Grayscale)
                {
                    if (info.BitDepth != 8)
                    {
                        throw new ArgumentException();
                    }
                    ConvertGreyScalePNG(reader.BaseStream, pngFile, output);
                }
                else
                {
                    throw new NotImplementedException();
                }
                //else// if(GXTFormat == GXTFormat.DXT)
                //{
                //    ConvertPNG(reader.BaseStream, pngFile, Path.ChangeExtension(filename, ""));
                //}
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        private static void ConvertPNG(Stream stream,PngReader pngFile,string output)
        {
            var info = pngFile.ImgInfo;
            ushort width = (ushort)info.Cols;
            ushort height = (ushort)info.Rows;
            using var writer = new BinaryWriter(File.OpenWrite(output));
            writer.Write(Encoding.ASCII.GetBytes("GXT\0"));
            //
            writer.Write(0x100003);
            //Texture count
            writer.Write(0x00000001);
            writer.Write(0x00000040);
            writer.Write(0x00200000);
            writer.Write(0x00000000);
            writer.Write(0x00000000);
            writer.Write(0x00000000);
            writer.Write(0x00000000);
            //Texture Length
            //writer.Write(0x00002000);
            writer.Write(width * height);
            writer.Write(0xFFFFFFFF);
            writer.Write(0x00000000);
            writer.Write(0x00000000);
            //p8 texture format (indexed 8bpp)
            writer.Write(0x87000000);
            //Texture Width(0x800) and Height(0x400)
            writer.Write(width);
            writer.Write(height);
            writer.Write(0x00000001);

            var data = new byte[width * height*4];
            var byteLine = width*4;
            var row = new byte[byteLine];
            for(int i = 0,start = 0;i < height; i++,start += byteLine)
            {
                pngFile.ReadRowByte(row, i);
                Buffer.BlockCopy(row,0,data, start, byteLine);
            }
            data = PostProcessing.SwizzleTexturePSV(data, width, height, PixelFormat.Format32bppArgb);
            data = DxtUtil.Dxt5Encode(data, width, height);
            writer.Write(data);
        }
        private static void ConvertGreyScalePNG(Stream stream,PngReader pngFile,string output)
        {
            var info = pngFile.ImgInfo;
            ushort width = (ushort)info.Cols;
            ushort height = (ushort)info.Rows;
            using var writer = new BinaryWriter(File.OpenWrite(output));
            writer.Write(Encoding.ASCII.GetBytes("GXT\0"));
            //
            writer.Write(0x100003);
            //Texture count
            writer.Write(0x00000001);
            writer.Write(0x00000040);
            writer.Write(width * height);
            writer.Write(0x00000000);
            writer.Write(0x00000001);
            writer.Write(0x00000000);
            writer.Write(0x00000040);
            //Texture Length
            //writer.Write(0x00002000);
            writer.Write(width * height);
            writer.Write(0xFFFFFFFF);
            writer.Write(0x00000000);
            if (TextureType == TextureType.Linear)
            {
                writer.Write(0x60000000);
            }
            else if (TextureType == TextureType.Swizzled)
            {
                writer.Write(0x00000000);
            }
            //p8 texture format (indexed 8bpp)
            writer.Write(0x00007000);
            //Texture Width(0x800) and Height(0x400)
            writer.Write(width);
            writer.Write(height);
            writer.Write(0x00000001);

            var data = new byte[width * height];
            var row = new byte[width];
            for(int i = 0,start = 0;i < height; i++,start += width)
            {
                pngFile.ReadRowByte(row, i);
                Buffer.BlockCopy(row,0,data, start, width);
            }
            if (TextureType == TextureType.Swizzled)
            {
                data = PostProcessing.SwizzleTexturePSV(data, width, height, PixelFormat.Format8bppIndexed);
            }
            writer.Write(data);
        }
        private static void ConvertIndexedPNG(Stream stream,PngReader pngFile,string output)
        {
            var info = pngFile.ImgInfo;
            ushort width = (ushort)info.Cols;
            ushort height = (ushort)info.Rows;
            using var writer = new BinaryWriter(File.OpenWrite(output));
            var realWidth = width;
            var realHeight = height;
            if (realWidth % 8 != 0)
            {
                realWidth += (ushort)(8 - (realWidth % 8));
            }
            if (realHeight % 8 != 0)
            {
                realHeight += (ushort)(8 - (realHeight % 8));
            }
            writer.Write(Encoding.ASCII.GetBytes("GXT\0"));
            //0x4
            writer.Write(0x10000003);
            //0x8 Texture count
            writer.Write(0x00000001);
            //0xC
            writer.Write(0x00000040);
            //Texture Length+Palette.size
            //0x10
            writer.Write(realWidth * realHeight + 0x400);
            writer.Write(0x00000000);
            writer.Write(0x00000001);
            writer.Write(0x00000000);
            writer.Write(0x00000040);
            //Texture Length
            writer.Write(realWidth * realHeight);
            writer.Write(0x00000000);
            writer.Write(0x00000000);
            if (TextureType == TextureType.Linear)
            {
                writer.Write(0x60000000);
            }
            else if (TextureType == TextureType.Swizzled)
            {
                writer.Write(0x00000000);
            }
            //p8 texture format (indexed 8bpp)
            writer.Write(0x95001000);
            //Texture Width(0x800) and Height(0x400)
            writer.Write(width);
            writer.Write(height);
            writer.Write(0x00000001);
            var data = new byte[realWidth * realHeight];
            {
                var row = new byte[width];
                for (int i = 0, start = 0; i < height; i++, start += realWidth)
                {
                    pngFile.ReadRowByte(row, i);
                    Buffer.BlockCopy(row, 0, data, start, width);
                }
            }
            if (TextureType == TextureType.Swizzled)
            {
                data = PostProcessing.SwizzleTexturePSV(data, realWidth, realHeight, PixelFormat.Format8bppIndexed);
            }
            writer.Write(data);
            var pchunk = pngFile.GetChunksList().GetById(PngChunkPLTE.ID)[0] as PngChunkPLTE;

            var colorCount = pchunk.GetNentries();
            var rgb = new int[4];
            var palette = new Color[colorCount];
            for(var i = 0;i < colorCount; i++)
            {
                pchunk.GetEntryRgb(i, rgb);
                palette[i] = Color.FromArgb(rgb[0], rgb[1], rgb[2]);
            }

            var achunk = pngFile.GetChunksList().GetById(PngChunkTRNS.ID)[0] as PngChunkTRNS;
            var alpha = achunk.GetPalletteAlpha();
            for (var i = 0; i < colorCount; i++)
            {
                palette[i] = Color.FromArgb(alpha[i], palette[i]);
            }
            for(var i = 0;i < palette.Length; i++)
            {
                var color = palette[i];
                writer.Write(color.B);
                writer.Write(color.G);
                writer.Write(color.R);
                writer.Write(color.A);
            }
        }
        private static ushort ReadUint16(BinaryReader reader)
        {
            var low = reader.ReadByte();
            var high = reader.ReadByte();
            return (ushort)(low | (high << 8));
        }
        private static void ConvertIndexedGXT(BinaryReader reader,int width,int height,string output)
        {
            var info = new ImageInfo(width, height, 8, false, false, true);

            using var writer = File.OpenWrite(output);
            reader.BaseStream.Position = 0x40 + width * height;
            var pngFile = new PngWriter(writer, info);

            var chunk = new PngChunkPLTE(info);
            reader.BaseStream.Position = 0x40 + width * height;
            chunk.SetNentries(256);
            var tchunk = new PngChunkTRNS(info);
            var alpha = new int[256];
            for (var i = 0; i < 256; i++)
            {
                var b = reader.ReadByte();
                var g = reader.ReadByte();
                var r = reader.ReadByte();
                var a = reader.ReadByte();

                alpha[i] = a;
                chunk.SetEntry(i, r, g, b);
            }
            tchunk.SetPalletteAlpha(alpha);

            pngFile.GetChunksList().Queue(chunk);
            pngFile.GetChunksList().Queue(tchunk);
            pngFile.CompLevel = 9;
            reader.BaseStream.Position = 0x40;
            var data = reader.ReadBytes(width * height);
            data = PostProcessing.UnswizzleTexturePSV(data, width, height, PixelFormat.Format8bppIndexed);
            var row = new byte[width];
            for (int i = 0, start = 0; i < height; i++, start += width)
            {
                Array.Copy(data, start, row, 0, width);
                pngFile.WriteRowByte(row, i);
            }
            pngFile.End();
        }
        private static void ConvertUBC3(BinaryReader reader,int width,int height,string output)
        {

            //var info = new ImageInfo(width, height, 8, true);

            //using var writer = File.OpenWrite(output);
            //var pngFile = new PngWriter(writer, info);

            //pngFile.CompLevel = 9;
            reader.BaseStream.Position = 0x40;
            var PixelData = DXTx.Decompress(reader, width * height, width, height, SceGxmTextureBaseFormat.UBC3);
            //var temp = File.ReadAllBytes("temp.dat");
            //for (int i = 0; i < PixelData.Length; i++)
            //{
            //    if (PixelData[i] != temp[i])
            //    {

            //    }
            //}
            PixelData = PostProcessing.UnswizzleTexturePSV(PixelData, width, height, PixelFormat.Format32bppArgb);
            //width *= 4;
            //var row = new byte[width];
            //for (int i = 0, start = 0; i < height; i++, start += width)
            //{
            //    Array.Copy(data, start, row, 0, width);
            //    pngFile.WriteRowByte(row, i);
            //}
            //pngFile.End();


            Bitmap texture = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = texture.LockBits(new Rectangle(0, 0, texture.Width, texture.Height), ImageLockMode.ReadWrite, texture.PixelFormat);

            byte[] pixelsForBmp = new byte[bmpData.Height * bmpData.Stride];
            int bitsPerPixel = Bitmap.GetPixelFormatSize(texture.PixelFormat);

            // TODO, taken from Scarlet: verify input stride/line size & copy length logic; *seems* to work okay now...?
            bool isCompressed = true;
            int lineSize, copySize;

            if ((bmpData.Width % 8) == 0 || isCompressed)
                lineSize = (bmpData.Width / (bitsPerPixel < 8 ? 2 : 1)) * (bitsPerPixel < 8 ? 1 : bitsPerPixel / 8);
            else
                lineSize = (PixelData.Length / bmpData.Height);

            if (texture.PixelFormat == System.Drawing.Imaging.PixelFormat.Format4bppIndexed)
                copySize = bmpData.Width / 2;
            else
                copySize = (bmpData.Width / (bitsPerPixel < 8 ? 2 : 1)) * (bitsPerPixel < 8 ? 1 : bitsPerPixel / 8);

            for (int y = 0; y < bmpData.Height; y++)
            {
                int srcOffset = y * lineSize;
                int dstOffset = y * bmpData.Stride;
                if (srcOffset >= PixelData.Length || dstOffset >= pixelsForBmp.Length) continue;
                Buffer.BlockCopy(PixelData, srcOffset, pixelsForBmp, dstOffset, copySize);
            }

            Marshal.Copy(pixelsForBmp, 0, bmpData.Scan0, pixelsForBmp.Length);
            texture.UnlockBits(bmpData);


            Bitmap realTexture = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(realTexture))
            {
                g.DrawImageUnscaled(texture, 0, 0);
            }

            realTexture.Save(output);
        }
        private static void ConvertDirect(BinaryReader reader,int width,int height,string output)
        {
            reader.BaseStream.Position = 0x40;

            var info = new ImageInfo(width, height, 8,false,true,false);

            using var writer = File.OpenWrite(output);
            var pngFile = new PngWriter(writer, info);

            pngFile.CompLevel = 9;
            for(int y = 0;y < height; y++)
            {
                var row = reader.ReadBytes(width);
                pngFile.WriteRowByte(row, y);
            }

            pngFile.End();
        }
        private static void ConvertGXT(BinaryReader reader,string output)
        {
            reader.BaseStream.Position = 0x34;
            var format = reader.ReadUInt32();

            var width = ReadUint16(reader);
            var height = ReadUint16(reader);

            reader.BaseStream.Position = 0x40;

            if(format == 0x95001000)
            {
                ConvertIndexedGXT(reader, width, height, output);
            }
            else if(format == 0x87000000)
            {
                ConvertUBC3(reader, width, height,output);
            }
            else if(format == 0x00007000)
            {
                ConvertDirect(reader, width, height, output);
            }
        }
    }
}