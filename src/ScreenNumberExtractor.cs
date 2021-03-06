using IronOcr;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace ForzaVinylPainting
{
    public class ScreenNumberExtractor
    {
        private const float TopPerc = 0.222f;
        private const float BottomPerc = 0.269f;
        private const float XLeftPerc = 0.08f;
        private const float XRightPerc = 0.155f;
        private const float YLeftPerc = 0.199f;

        private readonly Rectangle _xCropRect;
        private readonly Rectangle _yCropRect;

        private ScreenCapturer _capturer;
        private IronTesseract _engine;

        public ScreenNumberExtractor(float screenWidth, float screenHeight)
        {
            var topPixel = (int)(screenHeight * TopPerc);
            var bottomPixel = (int)(screenHeight * BottomPerc);
            var height = bottomPixel - topPixel;

            var xLeftPixel = (int)(screenWidth * XLeftPerc);
            var xRightPixel = (int)(screenWidth * XRightPerc);
            var width = xRightPixel - xLeftPixel;

            _xCropRect = new Rectangle(xLeftPixel, topPixel, width, height);

            var yLeftPixel = (int)(screenWidth * YLeftPerc);
            _yCropRect = new Rectangle(yLeftPixel, topPixel, width, height); // Assume same width and height as X

            _capturer = new ScreenCapturer();
            _engine = new IronTesseract();
            _engine.Configuration.TesseractVariables.Add("debug_file", "");
            _engine.Configuration.EngineMode = TesseractEngineMode.TesseractOnly;
            _engine.Configuration.WhiteListCharacters = "—-0123456789.";
            _engine.Configuration.PageSegmentationMode = TesseractPageSegmentationMode.RawLine;
            //_engine.Configuration.TesseractVersion = TesseractVersion.Tesseract4;
            //_engine.Configuration.EngineMode = TesseractEngineMode.LstmOnly;
            _engine.Language = OcrLanguage.English;
        }

        public async Task<Tuple<decimal, decimal>> GetBoth()
        {
            try
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var result = await GetBothInt();
                    if (result == null)
                    {
                        Console.WriteLine($"OCR failed getting both on attempt {attempt}");
                    }
                    else
                    {
                        return result;
                    }
                }

                // Give up and just return one last try
                return await GetBothInt();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public async Task<decimal?> GetSingle()
        {
            try
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var result = await GetSingleInt();
                    if (result == null)
                    {
                        Console.WriteLine($"OCR failed getting single on attempt {attempt}");
                    }
                    else
                    {
                        return result;
                    }
                }

                // Give up and just return one last try
                return await GetSingleInt();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        private async Task<Tuple<decimal, decimal>> GetBothInt()
        {
            var screen = _capturer.CaptureScreen();

            var xCropped = Crop(screen, _xCropRect);
            var yCropped = Crop(screen, _yCropRect);

            var xResult = GetOcrResult2(xCropped);
            var yResult = GetOcrResult2(yCropped);

            if (!xResult.HasValue || !yResult.HasValue)
            {
                return null;
            }

            return new Tuple<decimal, decimal>(xResult.Value, yResult.Value);
        }

        private async Task<decimal?> GetSingleInt()
        {
            var screen = _capturer.CaptureScreen();
            var xCropped = Crop(screen, _xCropRect);
            return GetOcrResult2(xCropped);
        }

        private decimal? GetOcrResult2(Bitmap img)
        {
            var input = new OcrInput(img);
            // input = input.Dilate();
            var s = _engine.Read(input);

            var text = s.Text;
            if (text.Length > 0)
            {
                if (text[text.Length - 1] == '-')
                {
                    text = text.Substring(0, text.Length - 1);
                }
            }

            Console.WriteLine(text);
            if (decimal.TryParse(text, out decimal result))
            {
                return result;
            }
            else
            {
                img.Save($"x{text}.png");
            }

            return 0;
        }

        private Bitmap Crop(Bitmap b, Rectangle r)
        {
            var bmpImage = new Bitmap(b);
            return bmpImage.Clone(r, bmpImage.PixelFormat);
        }
    }
}
