using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace CMCS.Tests
{
    internal sealed class FakeEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "CMCS";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;

        public FakeEnv(string root)
        {
            ContentRootPath = root;
            WebRootPath = Path.Combine(root, "wwwroot");
            Directory.CreateDirectory(WebRootPath);
        }
    }

    internal static class TestPaths
    {
        public static string NewTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "CMCS_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
