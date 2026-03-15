using System.Net;
using System.Net.Sockets;
using System.Text;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class PaperDocumentProcessingService(
    ApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment hostEnvironment,
    IOptions<DocumentProcessingOptions> options,
    ILogger<PaperDocumentProcessingService> logger)
{
    private readonly DocumentProcessingOptions _options = options.Value;

    public async Task<PaperDocument> ProcessAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.PaperDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperDocument), documentId);

        try
        {
            var sourceUri = await ValidateSourceUriAsync(document.SourceUrl, cancellationToken);

            using var client = httpClientFactory.CreateClient("PaperDocuments");
            using var response = await client.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var maxDownloadBytes = _options.MaxDownloadSizeMegabytes * 1024L * 1024L;
            if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maxDownloadBytes)
            {
                throw new InvalidStateException($"Document exceeds max allowed size of {_options.MaxDownloadSizeMegabytes} MB.");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? document.MediaType ?? GuessMediaType(document.SourceUrl, document.FileName);
            var fileName = document.FileName ?? DeriveFileName(document.SourceUrl, mediaType, document.Id);
            var bytes = await ReadBytesWithLimitAsync(response.Content, maxDownloadBytes, cancellationToken);

            var storageRoot = Path.IsPathRooted(_options.StorageRoot)
                ? _options.StorageRoot
                : Path.Combine(hostEnvironment.ContentRootPath, _options.StorageRoot);
            Directory.CreateDirectory(storageRoot);

            var targetDirectory = Path.Combine(storageRoot, document.PaperId.ToString("N"));
            Directory.CreateDirectory(targetDirectory);

            var targetPath = Path.Combine(targetDirectory, $"{document.Id:N}{Path.GetExtension(fileName)}");
            await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);

            document.FileName = fileName;
            document.MediaType = mediaType;
            document.StoragePath = targetPath;
            document.DownloadedAt = DateTimeOffset.UtcNow;
            document.Status = PaperDocumentStatus.Downloaded;
            document.LastError = null;

            var extractedText = ExtractText(bytes, mediaType, fileName);
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                document.ExtractedText = extractedText;
                document.ExtractedAt = DateTimeOffset.UtcNow;
                document.Status = PaperDocumentStatus.Extracted;
            }
            else if (document.RequiresOcr)
            {
                throw new InvalidStateException("OCR was requested for this document, but no OCR provider is configured yet.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Processed paper document {DocumentId} with status {Status}", document.Id, document.Status);
            return document;
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            document.Status = PaperDocumentStatus.Failed;
            document.LastError = QueryHelpers.Truncate(ex.Message, 4096);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveException)
            {
                logger.LogWarning(saveException, "Failed to persist failure state for paper document {DocumentId}", document.Id);
            }

            throw;
        }
    }

    private static async Task<byte[]> ReadBytesWithLimitAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var initialCapacity = content.Headers.ContentLength is > 0 and <= int.MaxValue
            ? (int)content.Headers.ContentLength.Value
            : 16 * 1024;
        using var buffer = new MemoryStream(initialCapacity);
        var chunk = new byte[16 * 1024];
        long totalRead = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxBytes)
            {
                throw new InvalidStateException($"Document exceeds max allowed size of {maxBytes / 1024L / 1024L} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static async Task<Uri> ValidateSourceUriAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidStateException("Document source URL must be an absolute http or https URL.");
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStateException("Document source URL must not target localhost.");
        }

        var addresses = IPAddress.TryParse(uri.Host, out var address)
            ? [address]
            : await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
        {
            throw new InvalidStateException("Document source URL must resolve to a public internet address.");
        }

        return uri;
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal && !address.IsIPv6Multicast && !address.IsIPv6SiteLocal && !address.IsIPv6Teredo;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            10 => false,
            127 => false,
            169 when bytes[1] == 254 => false,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => false,
            192 when bytes[1] == 168 => false,
            _ => true
        };
    }

    private static string? ExtractText(byte[] bytes, string? mediaType, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true || extension is ".txt" or ".md" or ".json" or ".csv")
        {
            return Encoding.UTF8.GetString(bytes);
        }

        if (extension == ".pdf" || string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = new MemoryStream(bytes);
            using var pdf = PdfDocument.Open(stream);
            return string.Join("\n\n", pdf.GetPages().Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t))).Trim();
        }

        return null;
    }

    private static string DeriveFileName(string sourceUrl, string? mediaType, Guid documentId)
    {
        var uri = new Uri(sourceUrl);
        var fileName = Path.GetFileName(uri.LocalPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        var extension = string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase) ? ".pdf" : ".bin";
        return $"{documentId:N}{extension}";
    }

    private static string GuessMediaType(string sourceUrl, string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? sourceUrl).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

}