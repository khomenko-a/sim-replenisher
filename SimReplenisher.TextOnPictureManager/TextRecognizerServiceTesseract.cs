using SimReplenisher.Domain.Interfaces;
using Tesseract;

namespace SimReplenisher.TextOnPictureManager
{
    public class TextRecognizerServiceTesseract : ITextRecognizerService
    {
        private static readonly string TESS_DATA_PATH = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        private const string LANGUAGE = "ukr";

        public async Task<bool> ContainsTextAsync(byte[] imageData, string text)
        {
            var foundedText = await RecognizeTextAsync(imageData);

            return foundedText.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> RecognizeTextAsync(byte[] imageData)
        {
            using var engine = new TesseractEngine(TESS_DATA_PATH, LANGUAGE);

            using var img = Pix.LoadFromMemory(imageData);
            using var page = engine.Process(img);

            return page.GetText();
        }
    }
}
