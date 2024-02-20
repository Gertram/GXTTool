#pragma warning disable CA1416 // Validate platform compatibility
using System;
using System.IO;
using System.Linq;
using System.Drawing;

namespace GXTTool
{
    class Program
    {
        readonly static byte[] magic = new byte[4] { 0x47, 0x58, 0x54, 0x00 };//GXT
        static void Main(string[] args)
        {
            args = new string[] { "ID00030.gxt", "ID00030.png" };
            if (args[0].EndsWith(".gxt"))
                readGXT(args[0], args[1]);
            else if (args[0].EndsWith(".png"))
                writeGXT(args[0], args[1]);
            else
            {
                Console.WriteLine("Input was not GXT or PNG, exiting...");
                return;
            }
        }

        static void readGXT(string fileName, string outName)
        {
            if (File.Exists(fileName))
            {
                using (var stream = File.Open(fileName, FileMode.Open))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        //GXT Header
                        if (!reader.ReadBytes(4).SequenceEqual(magic))
                        {
                            Console.WriteLine("Not a GXT file or invalid header");
                            return;
                        }
                        var version = reader.ReadBytes(4);
                        var numText = reader.ReadBytes(4);
                        var headerSize = reader.ReadBytes(4);
                        var numTextures = reader.ReadBytes(4);
                        var numP4 = reader.ReadBytes(4);
                        var numP8 = reader.ReadBytes(4);
                        reader.ReadBytes(4);//Padding

                        //Texture Header
                        var textureDataOffset = reader.ReadBytes(4);
                        var textureDataSize = reader.ReadBytes(4);
                        var paletteIndex = reader.ReadBytes(4);
                        reader.ReadBytes(4);//Padding
                        var textureType = reader.ReadBytes(4);
                        var textureBaseFormat = reader.ReadBytes(4);
                        var textureWidth = reader.ReadBytes(2);
                        var textureHeight = reader.ReadBytes(2);
                        var textureMipmaps = reader.ReadBytes(2);
                        reader.ReadBytes(2);//Padding

                        Int32 textureDataSizeConverted = BitConverter.ToInt32(textureDataSize);
                        Console.WriteLine("Image of size " + BitConverter.ToUInt16(textureWidth) + "x" + BitConverter.ToUInt16(textureHeight) + " found.\r\nData size: " + textureDataSizeConverted);
                        var imageData = reader.ReadBytes(textureDataSizeConverted);

                        Bitmap convertedImage = new Bitmap(BitConverter.ToUInt16(textureWidth), BitConverter.ToUInt16(textureHeight), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        for (int x = 0; x < convertedImage.Width; x++)
                        {
                            for (int y = 0; y < convertedImage.Height; y++)
                            {
                                //Console.WriteLine(x + "x" + y);
                                convertedImage.SetPixel(x, y,
                                    Color.FromArgb(
                                        imageData[(x * 4) + 3 + (y * convertedImage.Width * 4)],
                                        imageData[(x * 4) + 2 + (y * convertedImage.Width * 4)],
                                        imageData[(x * 4) + 1 + (y * convertedImage.Width * 4)],
                                        imageData[(x * 4) + 0 + (y * convertedImage.Width * 4)]
                                        )
                                    );
                            }
                        }

                        convertedImage.Save(outName,System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            }
        }

        static void writeGXT(string fileName, string outName)
        {
            Bitmap inputimage = new Bitmap(fileName);
            int datasize = inputimage.Width * inputimage.Height * 4;
            byte[] imageArray = new byte[datasize];

            for (int x = 0; x < inputimage.Width; x++)
            {
                for (int y = 0; y < inputimage.Height; y++)
                {
                    Color thisPixel = inputimage.GetPixel(x, y);
                    imageArray[(x * 4) + 3 + (y * inputimage.Width) * 4] = thisPixel.A;
                    imageArray[(x * 4) + 2 + (y * inputimage.Width) * 4] = thisPixel.R;
                    imageArray[(x * 4) + 1 + (y * inputimage.Width) * 4] = thisPixel.G;
                    imageArray[(x * 4) + 0 + (y * inputimage.Width) * 4] = thisPixel.B;
                }
            }

            using (var stream = File.Open(outName, FileMode.Create))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(new byte[] {
                        0x47, 0x58, 0x54, 0x00,    //Magic
                        0x03, 0x00, 0x00, 0x10,    //Version
                        0x01, 0x00, 0x00, 0x00,    //Number of textures
                        0x40, 0x00, 0x00, 0x00 }); //Texture offset

                    Console.WriteLine(BitConverter.ToString(BitConverter.GetBytes(datasize)));
                    writer.Write(BitConverter.GetBytes(datasize));

                    writer.Write(new byte[] {
                        0x00, 0x00, 0x00, 0x00,    //Number of P4
                        0x00, 0x00, 0x00, 0x00,    //Number of P8
                        0x00, 0x00, 0x00, 0x00,    //Padding
                        0x40, 0x00, 0x00, 0x00 }); //Offset of Texture

                    writer.Write(BitConverter.GetBytes(datasize));

                    writer.Write(new byte[] {
                        0xFF, 0xFF, 0xFF, 0xFF,   //Index of the palette
                        0x00, 0x00, 0x00, 0x00,   //Padding
                        0x00, 0x00, 0x00, 0x60,   //Texture Type
                        0x00, 0x10, 0x00, 0x0C}); //Texture Base Format

                    Console.WriteLine(BitConverter.ToString(BitConverter.GetBytes(Convert.ToInt16(inputimage.Width))));
                    writer.Write(BitConverter.GetBytes(Convert.ToInt16(inputimage.Width)));

                    Console.WriteLine(BitConverter.ToString(BitConverter.GetBytes(Convert.ToInt16(inputimage.Height))));
                    writer.Write(BitConverter.GetBytes(Convert.ToInt16(inputimage.Height)));

                    writer.Write(new byte[] { 0x01, 0x00, 0x00, 0x00 });

                    writer.Write(imageArray);
                }
            }
        }


    }
}