using System;
using System.Collections.Generic;
using System.IO;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Extend;
using FFMpegCore.Pipes;
using ImageMagick;

namespace SpinningImageCS
{
	class Program
	{
		static void MakeSpinningGif(MagickImage image, double rotationalPeriod, double framerate, string filename, bool overwrite = false)
		{
			double ticksPerFrame = 100/framerate;
			if (ticksPerFrame < 0 || ticksPerFrame % 0 > double.Epsilon*100)
			{
				throw new ArgumentException(
					$"100/frameRate must be a positive integer according to the gif standard, {ticksPerFrame} is not a positive integer");
			}
			var spinningFramesSource = new RawVideoPipeSource(MakeSpinningFrames(image, rotationalPeriod, framerate))
			{
				FrameRate = framerate
			};
			Console.WriteLine(FFMpegArguments
				.FromPipeInput(spinningFramesSource)
				.OutputToFile(filename, true, options => options
					.WithCustomArgument($"-filter_complex [0:v]scale=-2:{image.Height}:flags=bicubic,split[a][b];[a]palettegen[p];[b][p]paletteuse")
				)
				.ProcessSynchronously());

		}
		static IEnumerable<IVideoFrame> MakeSpinningFrames(MagickImage image, double rotationalPeriodSeconds, double frameRate)
		{
			return MakeSpinningFrames(image, (int) Math.Round(rotationalPeriodSeconds * frameRate, 0));
		}

		static IEnumerable<IVideoFrame> MakeSpinningFrames(MagickImage image, int frames)
		{
			var interval = Math.Tau / frames;
			for (double radians = 0; radians < Math.Tau; radians += interval)
			{
				yield return new BitmapVideoFrameWrapper(SpinImageToRadians(image, radians).ToBitmap());
			}
		}

		static MagickImage SpinImageToRadians(MagickImage image, double radians)
		{
			MagickImage spunImage = new(image);
			int imageWidth = (int) Math.Round(image.Width * Math.Cos(radians), 0);
			if (imageWidth < 0)
			{
				spunImage.Flop();
				imageWidth = -imageWidth;
			}
			var resizeGeo = new MagickGeometry(imageWidth > 0 ? imageWidth : 1, image.Height)
			{
				FillArea = true,
				IgnoreAspectRatio = true
			};
			spunImage.Resize(resizeGeo);
			spunImage.Extent(image.Width, image.Height, Gravity.Center);
			spunImage.BackgroundColor = MagickColors.Black;
			return spunImage;
		}
		static void Main(string[] args)
		{
			string imageFile;
			if (args.Length < 1 || !File.Exists(args[0]))
			{
				Console.WriteLine("Enter the path to the image you want spun");
				imageFile = Console.ReadLine();
				while(string.IsNullOrWhiteSpace(imageFile) || !File.Exists(imageFile))
				{
					Console.WriteLine($"Invalid Image Path {Path.GetFullPath(imageFile ?? string.Empty)} \n Enter the path to the image you want spun");
					imageFile = Console.ReadLine();
				}
			}
			else
			{
				imageFile = args[0];
			}

			double rotationPeriod;
			if (args.Length < 2 || !double.TryParse(args[1], out rotationPeriod))
			{
				Console.WriteLine("Enter the rotational Period for the image (s)");
				string rotationPeriodString = Console.ReadLine();
				while (string.IsNullOrWhiteSpace(rotationPeriodString) || !double.TryParse(rotationPeriodString, out rotationPeriod))
				{
					Console.WriteLine($"Invalid Image Seconds {rotationPeriodString}\n Enter the path to the image you want spun");
					rotationPeriodString = Console.ReadLine();
				}
			}
			else
			{
				rotationPeriod = double.Parse(args[1]);
			}
			var filename = "rimaspin.gif";
			if (Path.GetExtension(filename) != ".gif")
			{
				throw new ArgumentException(
					$"filename must have extension .gif, {filename}'s extenstion is {Path.GetExtension(filename)}");
			}

			bool overwrite = false;
			if (File.Exists(filename) && (args.Length < 3 || !bool.TryParse(args[2], out overwrite)))
			{
				Console.WriteLine($"File Already exists, Would you like to overwrite {Path.GetFullPath(filename)}?");
				string answer = Console.ReadLine();
				if (answer?.ToLower() is not ("y" or "yes"))
				{
					throw new ArgumentException(
						$"If you do not want to overwrite {Path.GetFullPath(filename)}, please specify a non-existent file");
				}
				overwrite = true;
			}
			MakeSpinningGif(new(imageFile), rotationPeriod, 100, filename, overwrite);
		}
	}
}
