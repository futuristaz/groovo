namespace Groovo.Services.Tus;

public class TusStorageConfiguration
{
    public string TempPath { get; }
    public string FinalPath { get; }

    public TusStorageConfiguration()
    {
        TempPath = "./uploads/tus/temp/";
        FinalPath = "./uploads/tus/final/";

        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(FinalPath);
    }
}
