using Microsoft.AspNetCore.Http;
using SmartOrderSystem.Controllers;

namespace SmartOrderSystem.Tests;

public class UploadValidationTests
{
    [Fact]
    public void ValidateUploadFiles_AllowsSupportedFilesWithinSizeLimit()
    {
        var provider = new MockFileProvider();
        var files = new List<IFormFile>
        {
            provider.CreateFormFile("sample.jpg", 1024, "image/jpeg"),
            provider.CreateFormFile("sample.png", 512, "image/png")
        };

        var result = ProductController.ValidateUploadFiles(files);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateUploadFiles_RejectsEmptyFileList()
    {
        var result = ProductController.ValidateUploadFiles(new List<IFormFile>());

        Assert.False(result.IsValid);
        Assert.Equal("Please select at least one image.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateUploadFiles_RejectsUnsupportedExtension()
    {
        var provider = new MockFileProvider();
        var files = new List<IFormFile>
        {
            provider.CreateFormFile("sample.gif", 1024, "image/gif")
        };

        var result = ProductController.ValidateUploadFiles(files);

        Assert.False(result.IsValid);
        Assert.Contains("invalid format", result.ErrorMessage);
    }

    [Fact]
    public void ValidateUploadFiles_RejectsOversizedFile()
    {
        var provider = new MockFileProvider();
        var files = new List<IFormFile>
        {
            provider.CreateFormFile("large.jpg", 6 * 1024 * 1024, "image/jpeg")
        };

        var result = ProductController.ValidateUploadFiles(files);

        Assert.False(result.IsValid);
        Assert.Contains("5MB", result.ErrorMessage);
    }

    private sealed class MockFileProvider
    {
        public IFormFile CreateFormFile(string fileName, int sizeInBytes, string contentType)
        {
            var bytes = new byte[sizeInBytes];
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "imageFiles", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
    }
}
