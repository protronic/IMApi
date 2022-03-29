using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using ImageMagick;

namespace IMApi.Controllers;

[ApiController]
[Route("[controller]")]
public class IMController : Controller
{
    private readonly ILogger<IMController> logger;
    private IWebHostEnvironment env;
    private MagickImageInfo imi;

    public IMController(ILogger<IMController> logger, IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
        this.imi = new MagickImageInfo();
    }

    [HttpGet(Name = "GetInfo")]
    public IEnumerable<IMagickFormatInfo> Get()
    {
        var fileInfos = new List<IMagickFormatInfo>();
        var provider = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images"));        
        foreach (PhysicalFileInfo file in provider.GetDirectoryContents(""))
        {
            var info = GetFormatInformation(new FileInfo(file.PhysicalPath));
            if (info != null)
                fileInfos.Add(info);
        }
        return fileInfos.AsEnumerable<IMagickFormatInfo>();
    }

    private static IMagickFormatInfo? GetFormatInformation(FileInfo file)
    {
        IMagickFormatInfo? info = MagickNET.GetFormatInformation(file);
        if (info != null)
            return info;

        MagickImageInfo imageInfo = new MagickImageInfo(file);
        return MagickNET.GetFormatInformation(imageInfo.Format);
    }
}
