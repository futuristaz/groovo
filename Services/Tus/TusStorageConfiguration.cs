namespace Groovo.Services.Tus;

public class TusStorageConfiguration
{
    public string TempPath { get; set; }
    public string FinalPath { get; set; }

    public TusStorageConfiguration()
    {
        TempPath = "./uploads/tus/temp/";
        FinalPath = "./uploads/tus/final/";

        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(FinalPath);
    }
}
