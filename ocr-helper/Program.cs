using System.Text;
using System.Text.Json;
using RapidOcrNet;
using SkiaSharp;

string baseDirectory = AppContext.BaseDirectory;
string detector = Path.Combine(baseDirectory, "models", "v6", "PP-OCRv6_det_small.onnx");
string recognizer = Path.Combine(baseDirectory, "models", "v6", "PP-OCRv6_rec_small.onnx");
string dictionary = Path.Combine(baseDirectory, "models", "v6", "ppocrv6_small_dict.txt");
string classifier = Path.Combine(baseDirectory, "models", "v5", "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");

using var ocr = new RapidOcr();
var models = RapidOcrModelSet.PPOCRv6Small with
{
    DetModelPath = detector,
    RecModelPath = recognizer,
    KeysPath = dictionary,
    ClsModelPath = classifier,
};
ocr.InitModels(models);

string Recognize(string imagePath)
{
    try
    {
        using SKBitmap bitmap = SKBitmap.Decode(imagePath) ?? throw new InvalidDataException("Unable to decode image");
        OcrResult result = ocr.Detect(bitmap, RapidOcrOptions.PPOCRv6);
        var blocks = result.TextBlocks
            .Where(block => block.BoxPoints.Max(point => point.Y) < bitmap.Height - 12)
            .Select(block => new
            {
                text = block.Text.Trim(),
                left = block.BoxPoints.Min(point => point.X),
                top = block.BoxPoints.Min(point => point.Y),
                right = block.BoxPoints.Max(point => point.X),
                bottom = block.BoxPoints.Max(point => point.Y),
            })
            .Where(block => block.text.Length > 0)
            .ToArray();
        return JsonSerializer.Serialize(new { imageWidth = bitmap.Width, imageHeight = bitmap.Height, blocks });
    }
    catch (Exception exception)
    {
        return JsonSerializer.Serialize(new { error = exception.GetType().Name + ": " + exception.Message });
    }
}

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

if (args.Length == 2)
{
    File.WriteAllText(args[1], Recognize(args[0]), new UTF8Encoding(false));
    return;
}

if (args.Length != 0)
{
    Console.Error.WriteLine("Usage: OcrHelper [<input.png> <output.json>]");
    return;
}

string? input;
while ((input = Console.ReadLine()) != null)
{
    try
    {
        string encodedPath = input.Trim().TrimStart('\uFEFF');
        string imagePath = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath));
        Console.WriteLine(Recognize(imagePath));
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error = exception.GetType().Name + ": " + exception.Message }));
    }
    Console.Out.Flush();
}
