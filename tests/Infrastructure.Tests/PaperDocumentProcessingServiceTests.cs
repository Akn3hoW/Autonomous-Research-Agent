using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PaperDocumentProcessingServiceTests : IDisposable
{
    [Fact]
    public async Task LocalDocumentTextExtractor_extracts_plain_text_bytes()
    {
        var extractor = new LocalDocumentTextExtractor();

        var result = await extractor.ExtractAsync(
            Encoding.UTF8.GetBytes("native extracted text"),
            "text/plain",
            "paper.txt",
            CancellationToken.None);

        Assert.Equal("native extracted text", result);
    }

    [Fact]
    public async Task ProcessAsync_uses_native_extraction_without_ocr_when_text_is_sufficient()
    {
        await using var dbContext = CreateDbContext();
        var document = CreateDocument("https://1.1.1.1/paper.txt", "paper.txt", "text/plain");
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            new FakeDocumentTextExtractor("native extraction is long enough to avoid OCR fallback"),
            CreateOptions(nonExistentOcrCommand: true));

        var processed = await service.ProcessAsync(document.Id, CancellationToken.None);

        Assert.Equal(PaperDocumentStatus.Extracted, processed.Status);
        Assert.Equal("native extraction is long enough to avoid OCR fallback", processed.ExtractedText);
        Assert.NotNull(processed.ExtractedAt);
        Assert.Null(processed.LastError);
    }

    [Fact]
    public async Task ProcessAsync_uses_ocr_when_native_extraction_is_missing()
    {
        await using var dbContext = CreateDbContext();
        var document = CreateDocument("https://1.1.1.1/paper.pdf", "paper.pdf", "application/pdf");
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            new FakeDocumentTextExtractor("   "),
            CreateOptions(ocrOutput: "ocr extracted text"));

        var processed = await service.ProcessAsync(document.Id, CancellationToken.None);

        Assert.Equal(PaperDocumentStatus.Extracted, processed.Status);
        Assert.Equal("ocr extracted text", processed.ExtractedText);
        Assert.NotNull(processed.ExtractedAt);
        Assert.Null(processed.LastError);
    }

    [Fact]
    public async Task ProcessAsync_uses_ocr_when_requires_ocr_is_true_even_if_native_text_exists()
    {
        await using var dbContext = CreateDbContext();
        var document = CreateDocument("https://1.1.1.1/paper.pdf", "paper.pdf", "application/pdf", requiresOcr: true);
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            new FakeDocumentTextExtractor("native text that would normally be enough"),
            CreateOptions(ocrOutput: "forced ocr text"));

        var processed = await service.ProcessAsync(document.Id, CancellationToken.None);

        Assert.Equal(PaperDocumentStatus.Extracted, processed.Status);
        Assert.Equal("forced ocr text", processed.ExtractedText);
        Assert.NotNull(processed.ExtractedAt);
        Assert.Null(processed.LastError);
    }

    [Fact]
    public async Task ProcessAsync_invokes_pdf_ocr_with_force_ocr_without_skip_text()
    {
        await using var dbContext = CreateDbContext();
        var document = CreateDocument("https://1.1.1.1/paper.pdf", "paper.pdf", "application/pdf", requiresOcr: true);
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var argumentsFilePath = Path.Combine(testDir, "args.txt");
        var service = CreateService(
            dbContext,
            new FakeDocumentTextExtractor(""),
            CreateOptions(ocrExecutablePath: CreateArgumentCapturingOcrScript(argumentsFilePath, "ocr extracted text from sidecar")));

        var processed = await service.ProcessAsync(document.Id, CancellationToken.None);

        var capturedArguments = await File.ReadAllLinesAsync(argumentsFilePath);
        Assert.Contains("--force-ocr", capturedArguments);
        Assert.DoesNotContain("--skip-text", capturedArguments);
        Assert.Contains("--sidecar", capturedArguments);
        Assert.Equal("ocr extracted text from sidecar", processed.ExtractedText);
    }

    [Fact]
    public async Task ProcessAsync_clears_stale_extracted_text_when_ocr_fails()
    {
        await using var dbContext = CreateDbContext();
        var document = CreateDocument("https://1.1.1.1/paper.pdf", "paper.pdf", "application/pdf", requiresOcr: true);
        document.Status = PaperDocumentStatus.Extracted;
        document.ExtractedText = "stale text";
        document.ExtractedAt = DateTimeOffset.UtcNow.AddDays(-1);
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            new FakeDocumentTextExtractor(""),
            CreateOptions(nonExistentOcrCommand: true));

        var ex = await Assert.ThrowsAsync<InvalidStateException>(() => service.ProcessAsync(document.Id, CancellationToken.None));

        Assert.Contains("OCR", ex.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await dbContext.PaperDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.Equal(PaperDocumentStatus.Failed, persisted.Status);
        Assert.Null(persisted.ExtractedText);
        Assert.Null(persisted.ExtractedAt);
        Assert.NotNull(persisted.LastError);
    }

    private static readonly string testDir = Path.Combine(Path.GetTempPath(), "ara-ocr-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, true);
    }

    private static PaperDocumentProcessingService CreateService(
        ApplicationDbContext dbContext,
        IDocumentTextExtractor extractor,
        IOptions<DocumentProcessingOptions> options)
    {
        return new PaperDocumentProcessingService(
            dbContext,
            new StubHttpClientFactory(CreateResponseMessage()),
            new StubHostEnvironment(),
            options,
            NullLogger<PaperDocumentProcessingService>.Instance,
            extractor);
    }

    private static PaperDocument CreateDocument(
        string sourceUrl,
        string fileName,
        string mediaType,
        bool requiresOcr = false)
    {
        return new PaperDocument
        {
            Paper = new Paper { Title = "Test paper" },
            SourceUrl = sourceUrl,
            FileName = fileName,
            MediaType = mediaType,
            RequiresOcr = requiresOcr,
            Status = PaperDocumentStatus.Pending
        };
    }

    private static IOptions<DocumentProcessingOptions> CreateOptions(
        bool nonExistentOcrCommand = false,
        string? ocrOutput = null,
        string? ocrExecutablePath = null)
    {
        var scriptPath = ocrExecutablePath
            ?? (nonExistentOcrCommand ? "/tmp/does-not-exist-ocr-command" : CreateOcrScript(ocrOutput ?? "ocr text"));

        return Options.Create(new DocumentProcessingOptions
        {
            StorageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            MaxDownloadSizeMegabytes = 10,
            OcrExecutablePath = scriptPath,
            OcrFallbackMinimumCharacters = 32
        });
    }

    private static string CreateOcrScript(string output)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ara-ocr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var scriptPath = Path.Combine(directory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ocr.cmd" : "ocr.sh");
        var script = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"@echo off\r\necho {output}\r\n"
            : $"#!/usr/bin/env bash\nset -e\nprintf '%s\\n' '{output.Replace("'", "'\"'\"'")}'\n";

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return scriptPath;
    }

    private static string CreateArgumentCapturingOcrScript(string argumentsFilePath, string sidecarOutput)
    {
        var directory = Path.GetDirectoryName(argumentsFilePath)
            ?? throw new InvalidOperationException("Arguments file path must include a directory.");
        Directory.CreateDirectory(directory);

        var scriptPath = Path.Combine(directory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ocr-args.cmd" : "ocr-args.py");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var script = $"""
                @echo off
                setlocal enabledelayedexpansion
                set "ARGS_FILE={argumentsFilePath}"
                if not exist "{directory}" mkdir "{directory}"
                > "!ARGS_FILE!" (
                for %%A in (%*) do echo %%~A
                )
                echo Windows test OCR harness does not support sidecar emulation 1>&2
                exit /b 1
                """;
            File.WriteAllText(scriptPath, script.Replace("\n", "\r\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        else
        {
            var escapedArgumentsPath = argumentsFilePath.Replace("'", "'\"'\"'");
            var escapedSidecarOutput = sidecarOutput.Replace("'", "'\"'\"'");
            var script = $$"""
                #!/usr/bin/env python3
                import pathlib
                import sys

                args = sys.argv[1:]
                args_path = pathlib.Path('{{escapedArgumentsPath}}')
                args_path.parent.mkdir(parents=True, exist_ok=True)
                args_path.write_text("\n".join(args))

                if "--skip-text" in args:
                    print("Choose only one of --force-ocr, --skip-text, --redo-ocr.", file=sys.stderr)
                    sys.exit(2)

                if "--sidecar" in args:
                    sidecar_path = pathlib.Path(args[args.index("--sidecar") + 1])
                    sidecar_path.write_text('{{escapedSidecarOutput}}')

                if args:
                    output_path = pathlib.Path(args[-1])
                    output_path.parent.mkdir(parents=True, exist_ok=True)
                    output_path.write_bytes(b"%PDF-1.4 test output")
                """;
            File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return scriptPath;
    }

    private static HttpResponseMessage CreateResponseMessage()
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("ignored"));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Headers.ContentLength = content.ReadAsByteArrayAsync().Result.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeDocumentTextExtractor(string text) : IDocumentTextExtractor
    {
        public Task<string?> ExtractAsync(byte[] bytes, string? mediaType, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(text);
    }

    private sealed class StubHttpClientFactory(HttpResponseMessage response) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHttpMessageHandler(response), disposeHandler: true);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response.Clone());
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Infrastructure.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal static class HttpResponseMessageExtensions
{
    public static HttpResponseMessage Clone(this HttpResponseMessage response)
    {
        var clone = new HttpResponseMessage(response.StatusCode)
        {
            ReasonPhrase = response.ReasonPhrase,
            Version = response.Version,
            RequestMessage = response.RequestMessage,
            Content = response.Content is null ? null : new ByteArrayContent(response.Content.ReadAsByteArrayAsync().Result)
        };

        foreach (var header in response.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (response.Content is not null && clone.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
