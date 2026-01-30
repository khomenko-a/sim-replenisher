using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;

using SimReplenisher.Domain.Interfaces;

namespace SimReplenisher.TextOnPictureManager
{
    public class TextRecognizerServiceOcr : ITextRecognizerService
    {
        public async Task<bool> ContainsTextAsync(byte[] imageData, string text)
        {
            var recognizedText = await RecognizeTextAsync(imageData);

            return recognizedText.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> RecognizeTextAsync(byte[] imageData)
        {
            var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("ru-RU"));
            if (engine == null)
            {
                throw new InvalidOperationException("OCR engine could not be created for the specified language.");
            }

            using var stream = new MemoryStream(imageData);
            var decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
            var bitmap = await decoder.GetSoftwareBitmapAsync();

            var ocrResult = await engine.RecognizeAsync(bitmap);

            return ocrResult.Text;
        }
    }
}
