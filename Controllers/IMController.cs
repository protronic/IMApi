using Microsoft.AspNetCore.Mvc;
using ImageMagick;

namespace IMApi.Controllers;

[ApiController]
[Route("[controller]")]
public class IMController : ControllerBase
{
    private readonly ILogger<IMController> logger;
    private IHostEnvironment env;
    private MagickImageInfo imi;

    public IMController(ILogger<IMController> logger, IHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
        this.imi = new MagickImageInfo();
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet(Name = "GetImages")]
    public IEnumerable<WeatherForecast> Get()
    {

        return null;
    }


    private void GetDirectories()
    {
        DataTable dt = new DataTable();
        dt.Columns.Add("direction", typeof(string));
        try
        {
            string[] dirs = Directory.GetDirectories(@"yourpath", "*", SearchOption.AllDirectories);
            foreach (string dir in dirs)
            {
                dt.Rows.Add(dir);
            }
            if (dirs.Length <= 0)
            {
                lbl.text = "your message"
    
        }

            rpt.DataSource = dt; //your repeater 
            rpt.DataBind(); //your repeater 
        }
        catch (Exception e)
        {
            lbl.text = "your message"//print message assign it to label
    }
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
